using ProtoBuf;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using static VRage.Game.MyObjectBuilder_EnvironmentDefinition;

namespace WelderWall.Data.Scripts.Math0424.WelderWall.Util
{
    /// <summary>
    /// Easily make terminal controls, requires a EasyNetworker session to be initalized
    /// Will handle syncing of the controls in MP
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class EasyTerminalControls<T> where T : IMyTerminalBlock
    {
        enum TerminalType
        {
            Slider,
            OnOff,
            Checkbox,
            ColorPicker,
            TextBox,
        }

        [ProtoContract]
        class TerminalControlUpdate
        {
            [ProtoMember(1)] public long EntityId;
            [ProtoMember(2)] public int TerminalId;
            [ProtoMember(3)] public TerminalType Type;
            [ProtoMember(8)] public string BlockType;

            [ProtoMember(4)] public float fValue;
            [ProtoMember(5)] public bool bValue;
            [ProtoMember(6)] public string tValue;
            [ProtoMember(7)] public Color cValue;

            public TerminalControlUpdate() { }
            public TerminalControlUpdate(long entityId, Type t, int terminalId, TerminalType type, object value)
            {
                EntityId = entityId;
                TerminalId = terminalId;
                Type = type;
                BlockType = t.FullName;
                switch (type)
                {
                    case TerminalType.Slider:
                        fValue = (float)value;
                        break;
                    case TerminalType.OnOff:
                        bValue = (bool)value;
                        break;
                    case TerminalType.Checkbox:
                        bValue = (bool)value;
                        break;
                    case TerminalType.ColorPicker:
                        cValue = (Color)value;
                        break;
                    case TerminalType.TextBox:
                        tValue = (string)value;
                        break;
                }
            }
        }

        private static ConcurrentDictionary<long, List<object>> TerminalData = new ConcurrentDictionary<long, List<object>>();
        private static ConcurrentDictionary<long, bool> TerminalEnabled = new ConcurrentDictionary<long, bool>();
        private static ConcurrentDictionary<string, List<IMyTerminalControl>> TerminalControls = new ConcurrentDictionary<string, List<IMyTerminalControl>>();
        private static Random r = new Random();

        //TODO sync this-- somehow..
        public void SetValues(IMyCubeBlock block, params object[] values)
        {
            TerminalData[block.EntityId] = values.ToList();
        }

        public void SetEnabled(IMyCubeBlock block, bool value)
        {
            TerminalEnabled[block.EntityId] = value;
        }

        static EasyTerminalControls()
        {
            EasyNetworker.RegisterPacket<TerminalControlUpdate>(UpdateTerminalControl);
        }

        private static void UpdateTerminalControl(TerminalControlUpdate update)
        {
            if (!TerminalData.ContainsKey(update.EntityId))
            {
                TerminalData.TryAdd(update.EntityId, new List<object>());
                for (int i = TerminalData.Count; i < update.TerminalId; i++)
                    TerminalData[update.EntityId].Add(null);
            }

            switch (update.Type)
            {
                case TerminalType.Slider:
                    TerminalData[update.EntityId][update.TerminalId] = update.fValue;
                    break;
                case TerminalType.OnOff:
                case TerminalType.Checkbox:
                    TerminalData[update.EntityId][update.TerminalId] = update.bValue;
                    break;
                case TerminalType.ColorPicker:
                    TerminalData[update.EntityId][update.TerminalId] = update.cValue;
                    break;
                case TerminalType.TextBox:
                    TerminalData[update.EntityId][update.TerminalId] = new StringBuilder(update.tValue);
                    break;
            }

            TerminalControls[update.BlockType][update.TerminalId].RedrawControl();
        }

        private int _terminalCount;

        private string _prefix;
        private string _subtypeId;

        public EasyTerminalControls(string prefix, string subtypeId)
        {
            _prefix = prefix;
            _subtypeId = subtypeId;
        }

        public EasyTerminalControls(string prefix, MyStringHash subtypeId)
        {
            _prefix = prefix;
            _subtypeId = subtypeId.String;
            string type = typeof(T).FullName;
            if (!TerminalControls.ContainsKey(type))
                TerminalControls[type] = new List<IMyTerminalControl>();
        }

        private bool IsVisible(IMyTerminalBlock b)
        {
            if (!TerminalData.ContainsKey(b.EntityId))
            {
                TerminalData.TryAdd(b.EntityId, new List<object>(_terminalCount));
                for (int i = 0; i < _terminalCount; i++)
                    TerminalData[b.EntityId].Add(null);
            }

            b.OnClose -= RemoveOnClose;
            b.OnClose += RemoveOnClose;

            return b.BlockDefinition.SubtypeId == _subtypeId;
        }

        private bool Enabled(IMyTerminalBlock b)
        {
            if (b is IMyFunctionalBlock)
                return ((IMyFunctionalBlock)b).Enabled && ((IMyFunctionalBlock)b).IsWorking;
            if (TerminalEnabled.ContainsKey(b.EntityId))
                return TerminalEnabled[b.EntityId];
            return true;
        }

        private void RemoveOnClose(IMyEntity ent)
        {
            TerminalData.Remove(ent.EntityId);
        }

        public EasyTerminalControls<T> WithSlider(string title, Action<IMyTerminalBlock, float, StringBuilder> builder, string tooltip, float min, float max, Action<T, float> changed = null)
        {
            int terminalId = _terminalCount;
            var terminal = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, T>($"{_prefix}_slider_{terminalId}");
            TerminalControls[typeof(T).FullName].Add(terminal);

            terminal.Title = MyStringId.GetOrCompute(title);
            terminal.Tooltip = MyStringId.GetOrCompute(tooltip);
            terminal.SupportsMultipleBlocks = true;
            
            terminal.Enabled += Enabled;
            terminal.Visible += IsVisible;
            terminal.Setter = (b, v) =>
            {
                float newVal = MathHelper.Clamp(v, min, max);
                EasyNetworker.SendToSyncRange(new TerminalControlUpdate(b.EntityId, typeof(T), terminalId, TerminalType.Slider, newVal), EasyNetworker.TransitType.ExcludeSender);
                TerminalData[b.EntityId][terminalId] = newVal;
                changed?.Invoke((T)b, newVal);
            };
            terminal.Getter = (b) =>
            {
                if (TerminalData[b.EntityId][terminalId] == null)
                    TerminalData[b.EntityId][terminalId] = min;

                return (float)TerminalData[b.EntityId][terminalId];
            };
            terminal.Writer = (b, sb) => builder?.Invoke(b, (float)TerminalData[b.EntityId][terminalId], sb);

            terminal.SetLimits(min, max);

            MyAPIGateway.TerminalControls.AddControl<T>(terminal);
            _terminalCount++;
            return this;
        }

        public EasyTerminalControls<T> WithOnOff(string title, string tooltip, string onText = "On", string offText = "Off", Action<T, bool> changed = null)
        {
            int terminalId = _terminalCount;
            var terminal = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, T>($"{_prefix}_onoff_{terminalId}");
            TerminalControls[typeof(T).FullName].Add(terminal);

            terminal.Title = MyStringId.GetOrCompute(title);
            terminal.Tooltip = MyStringId.GetOrCompute(tooltip);
            terminal.SupportsMultipleBlocks = true;

            terminal.OnText = MyStringId.GetOrCompute(onText);
            terminal.OffText = MyStringId.GetOrCompute(offText);

            terminal.Enabled += Enabled;
            terminal.Visible += IsVisible;
            terminal.Setter = (b, v) =>
            {
                EasyNetworker.SendToSyncRange(new TerminalControlUpdate(b.EntityId, typeof(T), terminalId, TerminalType.OnOff, v), EasyNetworker.TransitType.ExcludeSender);
                TerminalData[b.EntityId][terminalId] = v;
                changed?.Invoke((T)b, v);
            };
            terminal.Getter = (b) =>
            {
                if (TerminalData[b.EntityId][terminalId] == null)
                    TerminalData[b.EntityId][terminalId] = false;

                return (bool)TerminalData[b.EntityId][terminalId];
            };

            MyAPIGateway.TerminalControls.AddControl<T>(terminal);
            _terminalCount++;
            return this;
        }

        public EasyTerminalControls<T> WithCheckbox(string title, string tooltip, Action<T, bool> changed = null)
        {
            int terminalId = _terminalCount;
            var terminal = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, T>($"{_prefix}_onoff_{terminalId}");
            TerminalControls[typeof(T).FullName].Add(terminal);

            terminal.Title = MyStringId.GetOrCompute(title);
            terminal.Tooltip = MyStringId.GetOrCompute(tooltip);
            terminal.SupportsMultipleBlocks = true;
            
            terminal.Enabled += Enabled;
            terminal.Visible += IsVisible;
            terminal.Setter = (b, v) =>
            {
                EasyNetworker.SendToSyncRange(new TerminalControlUpdate(b.EntityId, typeof(T), terminalId, TerminalType.Checkbox, v), EasyNetworker.TransitType.ExcludeSender);
                TerminalData[b.EntityId][terminalId] = v;
                changed?.Invoke((T)b, v);
            };
            terminal.Getter = (b) =>
            {
                if (TerminalData[b.EntityId][terminalId] == null)
                    TerminalData[b.EntityId][terminalId] = false;

                return (bool)TerminalData[b.EntityId][terminalId];
            };

            MyAPIGateway.TerminalControls.AddControl<T>(terminal);
            _terminalCount++;
            return this;
        }

        public EasyTerminalControls<T> WithButton(string title, string tooltip, Action<T> clicked = null)
        {
            var terminal = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, T>($"{_prefix}_onoff_{r.Next()}");

            terminal.Title = MyStringId.GetOrCompute(title);
            terminal.Tooltip = MyStringId.GetOrCompute(tooltip);
            terminal.SupportsMultipleBlocks = true;

            terminal.Enabled += Enabled;
            terminal.Visible += IsVisible;
            terminal.Action += (b) => clicked?.Invoke((T)b);

            MyAPIGateway.TerminalControls.AddControl<T>(terminal);
            _terminalCount++;
            return this;
        }

        public EasyTerminalControls<T> WithColorPicker(string title, string tooltip, Action<T, Color> changed = null)
        {
            int terminalId = _terminalCount;
            var terminal = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlColor, T>($"{_prefix}_onoff_{terminalId}");
            TerminalControls[typeof(T).FullName].Add(terminal);

            terminal.Title = MyStringId.GetOrCompute(title);
            terminal.Tooltip = MyStringId.GetOrCompute(tooltip);
            terminal.SupportsMultipleBlocks = true;

            terminal.Enabled += Enabled;
            terminal.Visible += IsVisible;
            terminal.Setter = (b, v) =>
            {
                EasyNetworker.SendToSyncRange(new TerminalControlUpdate(b.EntityId, typeof(T), terminalId, TerminalType.ColorPicker, v), EasyNetworker.TransitType.ExcludeSender);
                TerminalData[b.EntityId][terminalId] = v;
                changed?.Invoke((T)b, v);
            };
            terminal.Getter = (b) =>
            {
                if (TerminalData[b.EntityId][terminalId] == null)
                    TerminalData[b.EntityId][terminalId] = Color.Red;

                return (Color)TerminalData[b.EntityId][terminalId];
            };

            MyAPIGateway.TerminalControls.AddControl<T>(terminal);
            _terminalCount++;
            return this;
        }

        public EasyTerminalControls<T> WithTextBox(string title, string tooltip, Action<T, string> changed = null)
        {
            int terminalId = _terminalCount;
            var terminal = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, T>($"{_prefix}_onoff_{terminalId}");
            TerminalControls[typeof(T).FullName].Add(terminal);

            terminal.Title = MyStringId.GetOrCompute(title);
            terminal.Tooltip = MyStringId.GetOrCompute(tooltip);
            terminal.SupportsMultipleBlocks = true;

            terminal.Enabled += Enabled;
            terminal.Visible += IsVisible;
            terminal.Setter = (b, v) =>
            {
                EasyNetworker.SendToSyncRange(new TerminalControlUpdate(b.EntityId, typeof(T), terminalId, TerminalType.TextBox, v.ToString()), EasyNetworker.TransitType.ExcludeSender);
                TerminalData[b.EntityId][terminalId] = v;
                changed?.Invoke((T)b, v.ToString());
            };
            terminal.Getter = (b) =>
            {
                if (TerminalData[b.EntityId][terminalId] == null)
                    TerminalData[b.EntityId][terminalId] = new StringBuilder();

                return (StringBuilder)TerminalData[b.EntityId][terminalId];
            };

            MyAPIGateway.TerminalControls.AddControl<T>(terminal);
            _terminalCount++;
            return this;
        }

        public EasyTerminalControls<T> WithLabel(string title)
        {
            var terminal = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlLabel, T>($"{_prefix}_label_{r.Next()}");
            terminal.Label = MyStringId.GetOrCompute(title);
            terminal.SupportsMultipleBlocks = true;
            terminal.Visible += IsVisible;
            MyAPIGateway.TerminalControls.AddControl<T>(terminal);
            return this;
        }

        public EasyTerminalControls<T> WithSeperator()
        {
            var terminal = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, T>($"{_prefix}_seperator_{r.Next()}");
            terminal.SupportsMultipleBlocks = true;
            terminal.Visible += IsVisible;
            MyAPIGateway.TerminalControls.AddControl<T>(terminal);
            return this;
        }

        public void WithAction(string title, string icon = @"Textures\GUI\Icons\Actions\CharacterToggle.dds")
        {
            int terminalId = _terminalCount;
            var action = MyAPIGateway.TerminalControls.CreateAction<T>($"{_prefix}_action_{terminalId}");
            action.Name = new StringBuilder(title);
            action.ValidForGroups = true;
            action.Icon = icon;
            action.Enabled += Enabled;
        }
    }

}
