using Microsoft.VisualBasic;
using ProtoBuf;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace WelderWall.Data.Scripts.Math0424.WelderWall.Util
{
    /// <summary>
    /// Version 1.0
    /// 
    /// Easily make terminal controls, requires a EasyNetworker session to be initalized
    /// Will handle syncing of the controls in MP
    /// You will handle saving and loading of the controls
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class EasyTerminalControls<T> where T : IMyTerminalBlock
    {
        enum TerminalType
        {
            None,
            Slider,
            OnOff,
            Checkbox,
            ColorPicker,
            TextBox,
        }

        [ProtoContract]
        class TerminalControlData
        {
            public TerminalControlData() { }
            public TerminalControlData(TerminalType type, int terminalId) 
            {
                Type = type;
                TerminalId = terminalId;
                Enabled = true;
                sValue = "";
            }

            public TerminalControlData(int terminalId, float fValue)
            {
                Type = TerminalType.Slider;
                Enabled = true;
                TerminalId = terminalId;
                this.fValue = fValue;
            }

            public TerminalControlData(TerminalType type, int terminalId, bool bValue)
            {
                Type = type;
                Enabled = true;
                TerminalId = terminalId;
                this.bValue = bValue;
            }

            public TerminalControlData(int terminalId, string sValue)
            {
                Type = TerminalType.TextBox;
                Enabled = true;
                TerminalId = terminalId;
                this.sValue = sValue;
            }

            public TerminalControlData(int terminalId, Color cValue)
            {
                Type = TerminalType.ColorPicker;
                Enabled = true;
                TerminalId = terminalId;
                this.cValue = cValue;
            }

            [ProtoMember(1)] public TerminalType Type;
            [ProtoMember(2)] public int TerminalId;
            [ProtoMember(3)] public bool Enabled;

            [ProtoMember(4)] public float fValue;
            [ProtoMember(5)] public bool bValue;
            [ProtoMember(6)] public string sValue;
            [ProtoMember(7)] public Color cValue;
        }

        [ProtoContract]
        class TerminalControlUpdate
        {
            [ProtoMember(1)] public long EntityId;
            [ProtoMember(2)] public string BlockType;
            [ProtoMember(3)] public TerminalControlData Data;

            public TerminalControlUpdate() { }
            public TerminalControlUpdate(long entityId, Type type, TerminalControlData data)
            {
                EntityId = entityId;
                Data = data;
                BlockType = type.FullName;
            }
        }

        private static ConcurrentDictionary<string, List<IMyTerminalControl>> TerminalControls = new ConcurrentDictionary<string, List<IMyTerminalControl>>();
        private static ConcurrentDictionary<long, List<TerminalControlData>> TerminalData = new ConcurrentDictionary<long, List<TerminalControlData>>();
        private static Random r = new Random();

        private static void PopulateData(long entityId, string blockType)
        {
            if (TerminalData.ContainsKey(entityId))
                return;

            TerminalData.TryAdd(entityId, new List<TerminalControlData>());
            for (int i = 0; i < TerminalControls[blockType].Count; i++)
            {
                Type t = TerminalControls[blockType][i].GetType();
                TerminalType type = TerminalType.None;
                if (MyAPIGateway.Reflection.IsAssignableFrom(typeof(IMyTerminalControlSlider), t))
                    type = TerminalType.Slider;
                else if (MyAPIGateway.Reflection.IsAssignableFrom(typeof(IMyTerminalControlOnOffSwitch), t))
                    type = TerminalType.OnOff;
                else if (MyAPIGateway.Reflection.IsAssignableFrom(typeof(IMyTerminalControlCheckbox), t))
                    type = TerminalType.Checkbox;
                else if (MyAPIGateway.Reflection.IsAssignableFrom(typeof(IMyTerminalControlColor), t))
                    type = TerminalType.ColorPicker;
                else if (MyAPIGateway.Reflection.IsAssignableFrom(typeof(IMyTerminalControlTextbox), t))
                    type = TerminalType.TextBox;
                TerminalData[entityId].Add(new TerminalControlData(type, i));
            }
        }

        public void SetValues(IMyCubeBlock block, params object[] values)
        {
            PopulateData(block.EntityId, _fullName);

            int i = 0;
            foreach (var x in TerminalData[block.EntityId])
            {
                switch(x.Type)
                {
                    case TerminalType.OnOff:
                    case TerminalType.Checkbox:
                        if (x.bValue != (bool)values[i])
                        {
                            x.bValue = (bool)values[i];
                            EasyNetworker.SendToSyncRange(new TerminalControlUpdate(block.EntityId, typeof(T), x), EasyNetworker.TransitType.ExcludeSender);
                        }
                        break;
                    case TerminalType.Slider:
                        if (x.fValue != (float)values[i])
                        {
                            x.fValue = (float)values[i];
                            EasyNetworker.SendToSyncRange(new TerminalControlUpdate(block.EntityId, typeof(T), x), EasyNetworker.TransitType.ExcludeSender);
                        }
                        break;
                    case TerminalType.ColorPicker:
                        if (x.cValue != (Color)values[i]) 
                        {
                            x.cValue = (Color)values[i];
                            EasyNetworker.SendToSyncRange(new TerminalControlUpdate(block.EntityId, typeof(T), x), EasyNetworker.TransitType.ExcludeSender);
                        }
                        break;
                    case TerminalType.TextBox:
                        if (x.sValue != (string)values[i]) 
                        {
                            x.sValue = (string)values[i];
                            EasyNetworker.SendToSyncRange(new TerminalControlUpdate(block.EntityId, typeof(T), x), EasyNetworker.TransitType.ExcludeSender);
                        }
                        break;
                    default:
                        break;
                }
                i++;
            }
        }

        public void SetAllEnabled(IMyCubeBlock block, bool value)
        {
            if (!TerminalData.ContainsKey(block.EntityId))
                return;

            foreach (var x in TerminalData[block.EntityId])
                x.Enabled = value;
        }

        static EasyTerminalControls()
        {
            EasyNetworker.RegisterPacket<TerminalControlUpdate>(UpdateTerminalControl);
        }

        private static void UpdateTerminalControl(TerminalControlUpdate update)
        {
            PopulateData(update.EntityId, update.BlockType);

            TerminalData[update.EntityId][update.Data.TerminalId] = update.Data;
            TerminalControls[update.BlockType][update.Data.TerminalId].RedrawControl();
        }

        private int _terminalCount;
        private string _prefix;
        private string _subtypeId;
        private string _fullName;

        public EasyTerminalControls(string prefix, string subtypeId)
        {
            _prefix = prefix;
            _subtypeId = subtypeId;
        }

        public EasyTerminalControls(string prefix, MyStringHash subtypeId)
        {
            _prefix = prefix;
            _subtypeId = subtypeId.String;
            _fullName = typeof(T).FullName;
            if (!TerminalControls.ContainsKey(_fullName))
                TerminalControls[_fullName] = new List<IMyTerminalControl>();
        }

        private bool IsVisible(IMyTerminalBlock b)
        {
            if (b.BlockDefinition.SubtypeId == _subtypeId)
            {
                PopulateData(b.EntityId, _fullName);
                b.OnClose -= RemoveOnClose;
                b.OnClose += RemoveOnClose;
                return true;
            }
            return false;
        }

        private bool Enabled(IMyTerminalBlock b, int id)
        {
            if (TerminalData.ContainsKey(b.EntityId))
                return TerminalData[b.EntityId][id].Enabled;
            if (b is IMyFunctionalBlock)
                return ((IMyFunctionalBlock)b).Enabled && ((IMyFunctionalBlock)b).IsWorking;
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
            TerminalControls[_fullName].Add(terminal);

            terminal.Title = MyStringId.GetOrCompute(title);
            terminal.Tooltip = MyStringId.GetOrCompute(tooltip);
            terminal.SupportsMultipleBlocks = true;
            
            terminal.Enabled += (b) => Enabled(b, terminalId);
            terminal.Visible += IsVisible;
            terminal.Setter = (b, v) =>
            {
                var data = TerminalData[b.EntityId][terminalId];
                data.fValue = MathHelper.Clamp(v, min, max);
                EasyNetworker.SendToSyncRange(new TerminalControlUpdate(b.EntityId, typeof(T), data), EasyNetworker.TransitType.ExcludeSender);
                TerminalData[b.EntityId][terminalId] = data;
                changed?.Invoke((T)b, data.fValue);
            };
            terminal.Getter = (b) => TerminalData[b.EntityId][terminalId].fValue;
            terminal.Writer = (b, sb) => builder?.Invoke(b, TerminalData[b.EntityId][terminalId].fValue, sb);

            terminal.SetLimits(min, max);

            MyAPIGateway.TerminalControls.AddControl<T>(terminal);
            _terminalCount++;
            return this;
        }

        public EasyTerminalControls<T> WithOnOff(string title, string tooltip, string onText = "On", string offText = "Off", Action<T, bool> changed = null)
        {
            int terminalId = _terminalCount;
            var terminal = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, T>($"{_prefix}_onoff_{terminalId}");
            TerminalControls[_fullName].Add(terminal);

            terminal.Title = MyStringId.GetOrCompute(title);
            terminal.Tooltip = MyStringId.GetOrCompute(tooltip);
            terminal.SupportsMultipleBlocks = true;

            terminal.OnText = MyStringId.GetOrCompute(onText);
            terminal.OffText = MyStringId.GetOrCompute(offText);

            terminal.Enabled += (b) => Enabled(b, terminalId);
            terminal.Visible += IsVisible;
            terminal.Setter = (b, v) =>
            {
                var data = TerminalData[b.EntityId][terminalId];
                data.bValue = v;
                EasyNetworker.SendToSyncRange(new TerminalControlUpdate(b.EntityId, typeof(T), data), EasyNetworker.TransitType.ExcludeSender);
                TerminalData[b.EntityId][terminalId] = data;
                changed?.Invoke((T)b, data.bValue);
            };
            terminal.Getter = (b) => TerminalData[b.EntityId][terminalId].bValue;

            MyAPIGateway.TerminalControls.AddControl<T>(terminal);
            _terminalCount++;
            return this;
        }

        public EasyTerminalControls<T> WithCheckbox(string title, string tooltip, Action<T, bool> changed = null)
        {
            int terminalId = _terminalCount;
            var terminal = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, T>($"{_prefix}_onoff_{terminalId}");
            TerminalControls[_fullName].Add(terminal);

            terminal.Title = MyStringId.GetOrCompute(title);
            terminal.Tooltip = MyStringId.GetOrCompute(tooltip);
            terminal.SupportsMultipleBlocks = true;

            terminal.Enabled += (b) => Enabled(b, terminalId);
            terminal.Visible += IsVisible;
            terminal.Setter = (b, v) =>
            {
                var data = TerminalData[b.EntityId][terminalId];
                data.bValue = v;
                EasyNetworker.SendToSyncRange(new TerminalControlUpdate(b.EntityId, typeof(T), data), EasyNetworker.TransitType.ExcludeSender);
                TerminalData[b.EntityId][terminalId] = data;
                changed?.Invoke((T)b, data.bValue);
            };
            terminal.Getter = (b) => TerminalData[b.EntityId][terminalId].bValue;

            MyAPIGateway.TerminalControls.AddControl<T>(terminal);
            _terminalCount++;
            return this;
        }

        public EasyTerminalControls<T> WithButton(string title, string tooltip, Action<T> clicked = null)
        {
            int terminalId = _terminalCount;
            var terminal = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, T>($"{_prefix}_button_{r.Next()}");
            TerminalControls[_fullName].Add(terminal);

            terminal.Title = MyStringId.GetOrCompute(title);
            terminal.Tooltip = MyStringId.GetOrCompute(tooltip);
            terminal.SupportsMultipleBlocks = true;

            terminal.Enabled += (b) => Enabled(b, terminalId);
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
            TerminalControls[_fullName].Add(terminal);

            terminal.Title = MyStringId.GetOrCompute(title);
            terminal.Tooltip = MyStringId.GetOrCompute(tooltip);
            terminal.SupportsMultipleBlocks = true;

            terminal.Enabled += (b) => Enabled(b, terminalId);
            terminal.Visible += IsVisible;
            terminal.Setter = (b, v) =>
            {
                var data = TerminalData[b.EntityId][terminalId];
                data.cValue = v;
                EasyNetworker.SendToSyncRange(new TerminalControlUpdate(b.EntityId, typeof(T), data), EasyNetworker.TransitType.ExcludeSender);
                TerminalData[b.EntityId][terminalId] = data;
                changed?.Invoke((T)b, data.cValue);
            };
            terminal.Getter = (b) => TerminalData[b.EntityId][terminalId].cValue;

            MyAPIGateway.TerminalControls.AddControl<T>(terminal);
            _terminalCount++;
            return this;
        }

        public EasyTerminalControls<T> WithTextBox(string title, string tooltip, Action<T, string> changed = null)
        {
            int terminalId = _terminalCount;
            var terminal = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, T>($"{_prefix}_onoff_{terminalId}");
            TerminalControls[_fullName].Add(terminal);

            terminal.Title = MyStringId.GetOrCompute(title);
            terminal.Tooltip = MyStringId.GetOrCompute(tooltip);
            terminal.SupportsMultipleBlocks = true;

            terminal.Enabled += (b) => Enabled(b, terminalId);
            terminal.Visible += IsVisible;
            terminal.Setter = (b, v) =>
            {
                var data = TerminalData[b.EntityId][terminalId];
                data.sValue = v.ToString();
                EasyNetworker.SendToSyncRange(new TerminalControlUpdate(b.EntityId, typeof(T), data), EasyNetworker.TransitType.ExcludeSender);
                TerminalData[b.EntityId][terminalId] = data;
                changed?.Invoke((T)b, data.sValue);
            };
            terminal.Getter = (b) => new StringBuilder(TerminalData[b.EntityId][terminalId].sValue);

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
            //action.Enabled += Enabled;
        }
    }

}
