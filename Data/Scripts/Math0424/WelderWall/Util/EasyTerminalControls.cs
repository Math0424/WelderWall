using Microsoft.VisualBasic;
using ProtoBuf;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using VRage;
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
        class RequestTerminalUpdate 
        {
            public RequestTerminalUpdate() { }

            public RequestTerminalUpdate(long entityId)
            {
                EntityId = entityId;
            }

            [ProtoMember(1)] public long EntityId;
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
            [ProtoMember(3)] public TerminalControlData[] Data;

            public TerminalControlUpdate() { }
            public TerminalControlUpdate(long entityId, Type type, params TerminalControlData[] data)
            {
                EntityId = entityId;
                Data = data;
                BlockType = type.FullName;
            }
            public TerminalControlUpdate(long entityId, string fullName, params TerminalControlData[] data)
            {
                EntityId = entityId;
                Data = data;
                BlockType = fullName;
            }
        }

        private static ConcurrentDictionary<string, List<MyTuple<IMyTerminalControl, Delegate>>> TerminalControls = new ConcurrentDictionary<string, List<MyTuple<IMyTerminalControl, Delegate>>>();
        private static ConcurrentDictionary<long, List<TerminalControlData>> TerminalData = new ConcurrentDictionary<long, List<TerminalControlData>>();
        private static ConcurrentDictionary<string, string> TerminalSubtypes = new ConcurrentDictionary<string, string>();
        private static Random r = new Random();

        private static void PopulateData(long entityId, string blockType)
        {
            if (TerminalData.ContainsKey(entityId))
                return;

            TerminalData.TryAdd(entityId, new List<TerminalControlData>());
            for (int i = 0; i < TerminalControls[blockType].Count; i++)
            {
                Type t = TerminalControls[blockType][i].Item1.GetType();
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

        /// <summary>
        /// Will only sync if the server calls it
        /// </summary>
        /// <param name="block"></param>
        /// <param name="values"></param>
        public void SetValues(IMyCubeBlock block, params object[] values)
        {
            PopulateData(block.EntityId, _fullName);

            int i = 0;
            var changed = new List<TerminalControlData>();
            foreach (var data in new List<TerminalControlData>(TerminalData[block.EntityId]))
            {
                switch (data.Type)
                {
                    case TerminalType.OnOff:
                    case TerminalType.Checkbox:
                        if (data.bValue != (bool)values[i])
                        {
                            data.bValue = (bool)values[i];
                            changed.Add(data);
                        }
                        break;
                    case TerminalType.Slider:
                        if (data.fValue != (float)values[i])
                        {
                            data.fValue = (float)values[i];
                            changed.Add(data);
                        }
                        break;
                    case TerminalType.ColorPicker:
                        if (data.cValue != (Color)values[i])
                        {
                            data.cValue = (Color)values[i];
                            changed.Add(data);
                        }
                        break;
                    case TerminalType.TextBox:
                        if (data.sValue != (string)values[i])
                        {
                            data.sValue = (string)values[i];
                            changed.Add(data);
                        }
                        break;
                }
                i++;
            }
            if (MyAPIGateway.Session.IsServer && changed.Count != 0)
                EasyNetworker.SendToSyncRange(new TerminalControlUpdate(block.EntityId, typeof(T), changed.ToArray()), block, EasyNetworker.TransitType.ExcludeSender);
        }

        /// <summary>
        /// Will only sync if the server calls it
        /// </summary>
        /// <param name="block"></param>
        /// <param name="value"></param>
        public void SetAllEnabled(IMyCubeBlock block, bool value)
        {
            if (!TerminalData.ContainsKey(block.EntityId))
                return;

            foreach (var data in TerminalData[block.EntityId])
            {
                if (data.Enabled != value)
                {
                    data.Enabled = value;
                    if (MyAPIGateway.Session.IsServer)
                        EasyNetworker.SendToSyncRange(new TerminalControlUpdate(block.EntityId, typeof(T), data), block, EasyNetworker.TransitType.ExcludeSender);
                }
            }
        }

        static EasyTerminalControls()
        {
            EasyNetworker.RegisterPacket<TerminalControlUpdate>(UpdateTerminalControl);
            EasyNetworker.RegisterPacket<RequestTerminalUpdate>(RequestedTerminalUpdate);
            MyAPIGateway.TerminalControls.CustomControlGetter += RequestUpdate;
        }

        private static void RequestUpdate(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            string subtype = block.BlockDefinition.SubtypeName;
            if (!TerminalSubtypes.ContainsKey(subtype))
                return;

            if (!MyAPIGateway.Session.IsServer)
                EasyNetworker.SendToServer(new RequestTerminalUpdate(block.EntityId));
        }

        private static void RequestedTerminalUpdate(ulong sender, RequestTerminalUpdate request)
        {
            if (!TerminalData.ContainsKey(request.EntityId))
                return;
            var ent = MyEntities.GetEntityById(request.EntityId);
            if (ent == null || !(ent is IMyCubeBlock))
                return;
            IMyCubeBlock block = ent as IMyCubeBlock;
            string subtype = block.BlockDefinition.SubtypeId.ToString();
            string name;
            if (!TerminalSubtypes.TryGetValue(subtype, out name))
                return;

            var data = TerminalData[request.EntityId];
            EasyNetworker.SendToPlayer(sender, new TerminalControlUpdate(request.EntityId, name, data.ToArray()));
        }

        private static void UpdateTerminalControl(ulong sender, TerminalControlUpdate request)
        {
            PopulateData(request.EntityId, request.BlockType);
            var ent = MyEntities.GetEntityById(request.EntityId);
            foreach (var update in request.Data)
            {
                TerminalData[request.EntityId][update.TerminalId] = update;
                if (ent != null && ent is IMyCubeBlock)
                {
                    switch (update.Type)
                    {
                        case TerminalType.Slider:
                            TerminalControls[request.BlockType][update.TerminalId].Item2?.DynamicInvoke(ent, update.fValue);
                            break;
                        case TerminalType.OnOff:
                        case TerminalType.Checkbox:
                            TerminalControls[request.BlockType][update.TerminalId].Item2?.DynamicInvoke(ent, update.bValue);
                            break;
                        case TerminalType.ColorPicker:
                            TerminalControls[request.BlockType][update.TerminalId].Item2?.DynamicInvoke(ent, update.cValue);
                            break;
                        case TerminalType.TextBox:
                            TerminalControls[request.BlockType][update.TerminalId].Item2?.DynamicInvoke(ent, update.sValue);
                            break;
                    }
                    TerminalControls[request.BlockType][update.TerminalId].Item1.UpdateVisual();
                }
            }

            if (ent != null && MyAPIGateway.Session.IsServer)
                EasyNetworker.SendToSyncRange(request, ent, EasyNetworker.TransitType.ExcludeSender);
        }

        private int _terminalCount;
        private string _prefix;
        private string _subtypeId;
        private string _fullName;

        public EasyTerminalControls(string prefix, MyStringHash subtypeId)
        {
            _prefix = prefix;
            _subtypeId = subtypeId.String;
            _fullName = typeof(T).FullName;
            TerminalSubtypes.TryAdd(_subtypeId, _fullName);
            if (!TerminalControls.ContainsKey(_fullName))
                TerminalControls[_fullName] = new List<MyTuple<IMyTerminalControl, Delegate>>();
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

        public EasyTerminalControls<T> WithSlider(string title, Action<IMyCubeBlock, float, StringBuilder> builder, string tooltip, float min, float max, Action<IMyCubeBlock, float> changed = null)
        {
            int terminalId = _terminalCount;
            var terminal = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, T>($"{_prefix}_slider_{terminalId}");
            TerminalControls[_fullName].Add(new MyTuple<IMyTerminalControl, Delegate>(terminal, changed));

            terminal.Title = MyStringId.GetOrCompute(title);
            terminal.Tooltip = MyStringId.GetOrCompute(tooltip);
            terminal.SupportsMultipleBlocks = true;
            
            terminal.Enabled += (b) => Enabled(b, terminalId);
            terminal.Visible += IsVisible;
            terminal.Setter = (b, v) =>
            {
                var data = TerminalData[b.EntityId][terminalId];
                data.fValue = MathHelper.Clamp(v, min, max);
                TerminalData[b.EntityId][terminalId] = data;
                if (MyAPIGateway.Session.IsServer)
                {
                    changed?.Invoke(b, data.fValue);
                    EasyNetworker.SendToSyncRange(new TerminalControlUpdate(b.EntityId, typeof(T), data), b, EasyNetworker.TransitType.ExcludeSender);
                }
                else
                    EasyNetworker.SendToServer(new TerminalControlUpdate(b.EntityId, typeof(T), data));
            };
            terminal.Getter = (b) => TerminalData[b.EntityId][terminalId].fValue;
            terminal.Writer = (b, sb) => builder?.Invoke(b, TerminalData[b.EntityId][terminalId].fValue, sb);

            terminal.SetLimits(min, max);

            MyAPIGateway.TerminalControls.AddControl<T>(terminal);
            _terminalCount++;
            return this;
        }

        public EasyTerminalControls<T> WithOnOff(string title, string tooltip, string onText = "On", string offText = "Off", Action<IMyCubeBlock, bool> changed = null)
        {
            int terminalId = _terminalCount;
            var terminal = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, T>($"{_prefix}_onoff_{terminalId}");
            TerminalControls[_fullName].Add(new MyTuple<IMyTerminalControl, Delegate>(terminal, changed));

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
                TerminalData[b.EntityId][terminalId] = data;
                if (MyAPIGateway.Session.IsServer)
                {
                    changed?.Invoke(b, data.bValue);
                    EasyNetworker.SendToSyncRange(new TerminalControlUpdate(b.EntityId, typeof(T), data), b, EasyNetworker.TransitType.ExcludeSender);
                }
                else
                    EasyNetworker.SendToServer(new TerminalControlUpdate(b.EntityId, typeof(T), data));
            };
            terminal.Getter = (b) => TerminalData[b.EntityId][terminalId].bValue;

            MyAPIGateway.TerminalControls.AddControl<T>(terminal);
            _terminalCount++;
            return this;
        }

        public EasyTerminalControls<T> WithCheckbox(string title, string tooltip, Action<IMyCubeBlock, bool> changed = null)
        {
            int terminalId = _terminalCount;
            var terminal = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, T>($"{_prefix}_onoff_{terminalId}");
            TerminalControls[_fullName].Add(new MyTuple<IMyTerminalControl, Delegate>(terminal, changed));

            terminal.Title = MyStringId.GetOrCompute(title);
            terminal.Tooltip = MyStringId.GetOrCompute(tooltip);
            terminal.SupportsMultipleBlocks = true;

            terminal.Enabled += (b) => Enabled(b, terminalId);
            terminal.Visible += IsVisible;
            terminal.Setter = (b, v) =>
            {
                var data = TerminalData[b.EntityId][terminalId];
                data.bValue = v;
                TerminalData[b.EntityId][terminalId] = data;
                if (MyAPIGateway.Session.IsServer)
                {
                    changed?.Invoke(b, data.bValue);
                    EasyNetworker.SendToSyncRange(new TerminalControlUpdate(b.EntityId, typeof(T), data), b, EasyNetworker.TransitType.ExcludeSender);
                }
                else
                    EasyNetworker.SendToServer(new TerminalControlUpdate(b.EntityId, typeof(T), data));
            };
            terminal.Getter = (b) => TerminalData[b.EntityId][terminalId].bValue;

            MyAPIGateway.TerminalControls.AddControl<T>(terminal);
            _terminalCount++;
            return this;
        }

        public EasyTerminalControls<T> WithColorPicker(string title, string tooltip, Action<IMyCubeBlock, Color> changed = null)
        {
            int terminalId = _terminalCount;
            var terminal = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlColor, T>($"{_prefix}_onoff_{terminalId}");
            TerminalControls[_fullName].Add(new MyTuple<IMyTerminalControl, Delegate>(terminal, changed));

            terminal.Title = MyStringId.GetOrCompute(title);
            terminal.Tooltip = MyStringId.GetOrCompute(tooltip);
            terminal.SupportsMultipleBlocks = true;

            terminal.Enabled += (b) => Enabled(b, terminalId);
            terminal.Visible += IsVisible;
            terminal.Setter = (b, v) =>
            {
                var data = TerminalData[b.EntityId][terminalId];
                data.cValue = v;
                TerminalData[b.EntityId][terminalId] = data;
                if (MyAPIGateway.Session.IsServer)
                {
                    changed?.Invoke(b, data.cValue);
                    EasyNetworker.SendToSyncRange(new TerminalControlUpdate(b.EntityId, typeof(T), data), b, EasyNetworker.TransitType.ExcludeSender);
                }
                else
                    EasyNetworker.SendToServer(new TerminalControlUpdate(b.EntityId, typeof(T), data));
            };
            terminal.Getter = (b) => TerminalData[b.EntityId][terminalId].cValue;

            MyAPIGateway.TerminalControls.AddControl<T>(terminal);
            _terminalCount++;
            return this;
        }

        public EasyTerminalControls<T> WithTextBox(string title, string tooltip, Action<IMyCubeBlock, string> changed = null)
        {
            int terminalId = _terminalCount;
            var terminal = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, T>($"{_prefix}_onoff_{terminalId}");
            TerminalControls[_fullName].Add(new MyTuple<IMyTerminalControl, Delegate>(terminal, changed));

            terminal.Title = MyStringId.GetOrCompute(title);
            terminal.Tooltip = MyStringId.GetOrCompute(tooltip);
            terminal.SupportsMultipleBlocks = true;

            terminal.Enabled += (b) => Enabled(b, terminalId);
            terminal.Visible += IsVisible;
            terminal.Setter = (b, v) =>
            {
                var data = TerminalData[b.EntityId][terminalId];
                data.sValue = v.ToString();
                TerminalData[b.EntityId][terminalId] = data;
                if (MyAPIGateway.Session.IsServer)
                {
                    changed?.Invoke(b, data.sValue);
                    EasyNetworker.SendToSyncRange(new TerminalControlUpdate(b.EntityId, typeof(T), data), b, EasyNetworker.TransitType.ExcludeSender);
                }
                else
                    EasyNetworker.SendToServer(new TerminalControlUpdate(b.EntityId, typeof(T), data));
            };
            terminal.Getter = (b) => new StringBuilder(TerminalData[b.EntityId][terminalId].sValue);

            MyAPIGateway.TerminalControls.AddControl<T>(terminal);
            _terminalCount++;
            return this;
        }

        public EasyTerminalControls<T> WithButton(string title, string tooltip, Action<IMyCubeBlock, bool> changed = null)
        {
            int terminalId = _terminalCount;
            var terminal = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, T>($"{_prefix}_button_{r.Next()}");
            TerminalControls[_fullName].Add(new MyTuple<IMyTerminalControl, Delegate>(terminal, changed));

            terminal.Title = MyStringId.GetOrCompute(title);
            terminal.Tooltip = MyStringId.GetOrCompute(tooltip);
            terminal.SupportsMultipleBlocks = true;

            terminal.Enabled += (b) => Enabled(b, terminalId);
            terminal.Visible += IsVisible;
            terminal.Action += (b) => changed?.Invoke((T)b, true);

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
