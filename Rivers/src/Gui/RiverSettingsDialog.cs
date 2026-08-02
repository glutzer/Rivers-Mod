using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Client;

namespace Rivers;

public class RiverSettingsDialog : GuiDialog
{
    private readonly ICoreClientAPI capi;
    private readonly Action<string> onApply;
    private readonly Action onClearMap;
    private readonly List<FieldInfo> editableFields;
    private readonly Dictionary<string, string> valueInputs = [];
    private readonly Dictionary<string, bool> boolValues = [];

    public override string ToggleKeyCombinationCode => null!;

    public RiverSettingsDialog(ICoreClientAPI capi, Action<string> onApply, Action onClearMap) : base(capi)
    {
        this.capi = capi;
        this.onApply = onApply;
        this.onClearMap = onClearMap;
        editableFields = [.. typeof(RiverConfig)
            .GetFields(BindingFlags.Instance | BindingFlags.Public)
            .Where(field => !field.IsInitOnly && !field.IsLiteral)];
    }

    public void OpenWithConfig(string json)
    {
        RiverConfig config = JsonConvert.DeserializeObject<RiverConfig>(json) ?? new RiverConfig();
        LoadStateFromConfig(config);

        RecomposeKeepingOpenState();

        TryOpen();
    }

    private void ComposeDialog()
    {
        const double dialogWidth = 1080;
        const double dialogHeight = 760;
        const double columnWidth = 500;
        const double rowHeight = 30;
        const double firstColumnX = 20;
        const double secondColumnX = 540;
        const double startY = 55;

        ElementBounds dialogBounds = ElementBounds.Fixed(0, 0, dialogWidth, dialogHeight).WithAlignment(EnumDialogArea.CenterMiddle);
        ElementBounds bgBounds = ElementBounds.Fixed(0, 0, dialogWidth, dialogHeight);
        ElementBounds titleTextBounds = ElementBounds.Fixed(20, 35, 900, 20);
        ElementBounds applyButtonBounds = ElementBounds.Fixed(20, dialogHeight - 45, 120, 30);
        ElementBounds cancelButtonBounds = ElementBounds.Fixed(150, dialogHeight - 45, 120, 30);
        ElementBounds clearMapButtonBounds = ElementBounds.Fixed(280, dialogHeight - 45, 140, 30);
        ElementBounds resetConfigButtonBounds = ElementBounds.Fixed(430, dialogHeight - 45, 160, 30);

        SingleComposer = capi.Gui
            .CreateCompo("riversettingsdialog", dialogBounds);

        SingleComposer = SingleComposer
            .AddShadedDialogBG(bgBounds, true)
            .AddDialogTitleBar("River Settings", OnTitleBarClose)
            .BeginChildElements(bgBounds)
            .AddStaticText("Edit river settings values and click Apply.", CairoFont.WhiteDetailText(), titleTextBounds)
            .Execute(() => AddFieldControls(firstColumnX, secondColumnX, startY, rowHeight, columnWidth))
            .AddSmallButton("Apply", OnApplyClicked, applyButtonBounds)
            .AddSmallButton("Cancel", OnCancelClicked, cancelButtonBounds)
            .AddSmallButton("Clear Map", OnClearMapClicked, clearMapButtonBounds)
            .AddSmallButton("Reset Config", OnResetConfigClicked, resetConfigButtonBounds)
            .EndChildElements()
            .Compose();

        foreach (FieldInfo field in editableFields)
        {
            if (field.FieldType == typeof(bool)) continue;

            string key = GetInputKey(field.Name);
            if (!valueInputs.TryGetValue(field.Name, out string? value))
            {
                value = string.Empty;
            }

            SingleComposer.GetTextInput(key)?.SetValue(value);
        }
    }

    private void AddFieldControls(double firstColumnX, double secondColumnX, double startY, double rowHeight, double columnWidth)
    {
        for (int i = 0; i < editableFields.Count; i++)
        {
            FieldInfo field = editableFields[i];
            int row = i / 2;
            bool rightColumn = (i % 2) == 1;

            double x = rightColumn ? secondColumnX : firstColumnX;
            double y = startY + (row * rowHeight);

            ElementBounds labelBounds = ElementBounds.Fixed(x, y + 6, 250, 20);
            SingleComposer!.AddStaticText(field.Name, CairoFont.WhiteSmallText(), labelBounds);

            if (field.FieldType == typeof(bool))
            {
                ElementBounds boolButtonBounds = ElementBounds.Fixed(x + 260, y, 210, 26);
                string fieldName = field.Name;

                SingleComposer.AddSmallButton(GetBoolButtonText(fieldName), () => OnBoolToggle(fieldName), boolButtonBounds);

                continue;
            }

            ElementBounds inputBounds = ElementBounds.Fixed(x + 260, y, columnWidth - 280, 26);
            string inputKey = GetInputKey(field.Name);
            string fieldKey = field.Name;

            SingleComposer.AddTextInput(inputBounds, text => OnValueChanged(fieldKey, text), CairoFont.WhiteSmallText(), inputKey);
        }
    }

    private void OnTitleBarClose()
    {
        TryClose();
    }

    private void OnValueChanged(string fieldName, string value)
    {
        valueInputs[fieldName] = value;
    }

    private bool OnApplyClicked()
    {
        if (!TryBuildConfig(out RiverConfig? config, out string? error))
        {
            capi.ShowChatMessage(error ?? "Failed to parse one or more values.");
            return true;
        }

        string json = JsonConvert.SerializeObject(config, Formatting.Indented);
        onApply(json);
        TryClose();
        return true;
    }

    private bool OnCancelClicked()
    {
        TryClose();
        return true;
    }

    private bool OnClearMapClicked()
    {
        onClearMap();
        return true;
    }

    private bool OnResetConfigClicked()
    {
        LoadStateFromConfig(new RiverConfig());
        RecomposeKeepingOpenState();
        return true;
    }

    private void LoadStateFromConfig(RiverConfig config)
    {
        valueInputs.Clear();
        boolValues.Clear();

        foreach (FieldInfo field in editableFields)
        {
            object? value = field.GetValue(config);

            if (field.FieldType == typeof(bool))
            {
                boolValues[field.Name] = value is bool b && b;
                continue;
            }

            valueInputs[field.Name] = value switch
            {
                float f => f.ToString(CultureInfo.InvariantCulture),
                double d => d.ToString(CultureInfo.InvariantCulture),
                _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
            };
        }
    }

    private bool OnBoolToggle(string fieldName)
    {
        bool current = boolValues.TryGetValue(fieldName, out bool value) && value;
        boolValues[fieldName] = !current;
        RecomposeKeepingOpenState();
        return true;
    }

    private void RecomposeKeepingOpenState()
    {
        bool wasOpen = IsOpened();
        SingleComposer?.Dispose();
        ComposeDialog();

        if (wasOpen)
        {
            TryOpen();
        }
    }

    private bool TryBuildConfig(out RiverConfig? config, out string? error)
    {
        config = new RiverConfig();
        error = null;

        foreach (FieldInfo field in editableFields)
        {
            try
            {
                if (field.FieldType == typeof(bool))
                {
                    bool parsedBool = boolValues.TryGetValue(field.Name, out bool value) && value;
                    field.SetValue(config, parsedBool);
                    continue;
                }

                string input = valueInputs.TryGetValue(field.Name, out string? text) ? text : string.Empty;
                object parsed = ParseFieldValue(field.FieldType, input);
                field.SetValue(config, parsed);
            }
            catch
            {
                error = $"Invalid value for '{field.Name}'.";
                config = null;
                return false;
            }
        }

        return true;
    }

    private static object ParseFieldValue(Type fieldType, string input)
    {
        return fieldType == typeof(int)
            ? int.Parse(input, CultureInfo.InvariantCulture)
            : fieldType == typeof(float)
            ? float.Parse(input, CultureInfo.InvariantCulture)
            : fieldType == typeof(double)
            ? (object)double.Parse(input, CultureInfo.InvariantCulture)
            : throw new NotSupportedException($"Unsupported field type {fieldType.Name}.");
    }

    private static string GetInputKey(string fieldName) => $"input_{fieldName}";

    private string GetBoolButtonText(string fieldName)
    {
        bool value = boolValues.TryGetValue(fieldName, out bool current) && current;
        return value ? "☑ True" : "☐ False";
    }
}
