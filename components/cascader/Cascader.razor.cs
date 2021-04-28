﻿using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using System.Linq;
using System.Collections;
using System.Threading.Tasks;

namespace AntDesign
{
    public partial class Cascader : AntInputComponentBase<string>
    {
        [Parameter] public bool Readonly { get; set; } = true;

        /// <summary>
        /// 是否支持清除
        /// </summary>
        [Parameter] public bool AllowClear { get; set; } = true;

        /// <summary>
        /// 是否显示关闭图标
        /// </summary>
        private bool ShowClearIcon { get; set; }

        /// <summary>
        /// 当此项为 true 时，点选每级菜单选项值都会发生变化
        /// </summary>
        [Parameter] public bool ChangeOnSelect { get; set; }

        /// <summary>
        /// 默认的选中项
        /// </summary>
        [Parameter] public string DefaultValue { get; set; }

        /// <summary>
        /// 次级菜单的展开方式，可选 'click' 和 'hover'
        /// </summary>
        [Parameter] public string ExpandTrigger { get; set; }

        /// <summary>
        /// 当下拉列表为空时显示的内容
        /// </summary>
        [Parameter] public string NotFoundContent { get; set; } = "Not Found";

        /// <summary>
        /// 输入框占位文本
        /// </summary>
        [Parameter] public string PlaceHolder { get; set; } = "请选择";

        [Parameter] public string PopupContainerSelector { get; set; } = "body";

        /// <summary>
        /// 在选择框中显示搜索框
        /// </summary>
        [Parameter] public bool ShowSearch { get; set; }

        /// <summary>
        /// Please use SelectedNodesChanged instead.
        /// </summary>
        [Obsolete("Instead use SelectedNodesChanged.")]
        [Parameter] public Action<List<CascaderNode>, string, string> OnChange { get; set; }

        [Parameter] public EventCallback<CascaderNode[]> SelectedNodesChanged { get; set; }

        [Parameter]
        public IReadOnlyCollection<CascaderNode> Options
        {
            get
            {
                if (_nodelist != null)
                    return _nodelist;
                return Array.Empty<CascaderNode>();
            }
            set
            {
                if (value == null || value.Count == 0)
                {
                    _nodelist = null;
                    return;
                }
                if (_nodelist == null) _nodelist = new List<CascaderNode>();
                else if (_nodelist.Count != 0) _nodelist.Clear();
                _nodelist.AddRange(value);

                ProcessParentAndDefault();
            }
        }

        private List<CascaderNode> _nodelist;

        /// <summary>
        /// 选中节点集合(click)
        /// </summary>
        internal List<CascaderNode> _selectedNodes = new List<CascaderNode>();

        /// <summary>
        /// 选中节点集合(hover)
        /// </summary>
        internal List<CascaderNode> _hoverSelectedNodes = new List<CascaderNode>();

        /// <summary>
        /// 用于渲染下拉节点集合(一级节点除外)
        /// </summary>
        internal List<CascaderNode> _renderNodes = new List<CascaderNode>();

        private ClassMapper _menuClassMapper = new ClassMapper();
        private ClassMapper _inputClassMapper = new ClassMapper();

        /// <summary>
        /// 浮层 展开/折叠状态
        /// </summary>
        private bool ToggleState { get; set; }

        /// <summary>
        /// 鼠标是否处于 浮层 之上
        /// </summary>
        private bool IsOnCascader { get; set; }

        /// <summary>
        /// 选择节点类型
        /// Click: 点击选中节点, Hover: 鼠标移入选中节点
        /// </summary>
        private SelectedTypeEnum SelectedType { get; set; }

        private string _displayText;
        private bool _initialized;

        private static Dictionary<string, string> _sizeMap = new Dictionary<string, string>()
        {
            ["large"] = "lg",
            ["small"] = "sm"
        };

        protected override void OnInitialized()
        {
            base.OnInitialized();

            ClassMapper
                .Add("ant-cascader-picker")
                .GetIf(() => $"ant-cascader-picker-{Size}", () => _sizeMap.ContainsKey(Size))
                .If("ant-cascader-picker-rtl", () => RTL);

            _inputClassMapper
                .Add("ant-input")
                .Add("ant-cascader-input")
                .GetIf(() => $"ant-cascader-input-{_sizeMap[Size]}", () => _sizeMap.ContainsKey(Size))
                .If("ant-cascader-input-rtl", () => RTL);

            _menuClassMapper
                .Add("ant-cascader-menu")
                .If($"ant-cascader-menu-rtl", () => RTL);

            SetDefaultValue(Value ?? DefaultValue);
        }

        protected override void OnValueChange(string value)
        {
            base.OnValueChange(value);

            RefreshNodeValue(value);
        }

        #region event

        /// <summary>
        /// 输入框单击(显示/隐藏浮层)
        /// </summary>
        private void InputOnToggle()
        {
            SelectedType = SelectedTypeEnum.Click;
            _hoverSelectedNodes.Clear();
            ToggleState = !ToggleState;
        }

        /// <summary>
        /// 输入框/浮层失去焦点(隐藏浮层)
        /// </summary>
        private void CascaderOnBlur()
        {
            if (!IsOnCascader)
            {
                ToggleState = false;
                _renderNodes = _selectedNodes;
            }
        }

        /// <summary>
        /// 输入框鼠标移入
        /// </summary>
        private void InputOnMouseOver()
        {
            if (!AllowClear) return;

            ShowClearIcon = true;
        }

        /// <summary>
        /// 输入框鼠标移出
        /// </summary>
        private void InputOnMouseOut()
        {
            if (!AllowClear) return;

            ShowClearIcon = false;
        }

        /// <summary>
        /// 清除已选择项
        /// </summary>
        private void ClearSelected()
        {
            _selectedNodes.Clear();
            _hoverSelectedNodes.Clear();
            _displayText = string.Empty;
            SetValue(string.Empty);
        }

        /// <summary>
        /// 浮层移入
        /// </summary>
        private void NodesOnMouseOver()
        {
            if (!AllowClear) return;

            IsOnCascader = true;
        }

        /// <summary>
        /// 浮层移出
        /// </summary>
        private void NodesOnMouseOut()
        {
            if (!AllowClear) return;

            IsOnCascader = false;
        }

        /// <summary>
        /// 下拉节点单击
        /// </summary>
        /// <param name="node"></param>
        private void NodeOnClick(CascaderNode node)
        {
            if (node.Disabled) return;

            SetSelectedNode(node, SelectedTypeEnum.Click);
        }

        /// <summary>
        /// 下拉节点移入
        /// </summary>
        /// <param name="node"></param>
        private void NodeOnMouseOver(CascaderNode node)
        {
            if (ExpandTrigger != "hover") return;

            if (node.Disabled) return;
            if (!node.HasChildren) return;

            SetSelectedNode(node, SelectedTypeEnum.Hover);
        }

        #endregion event

        /// <summary>
        /// 选中节点
        /// </summary>
        /// <param name="cascaderNode"></param>
        /// <param name="selectedType"></param>
        internal void SetSelectedNode(CascaderNode cascaderNode, SelectedTypeEnum selectedType)
        {
            if (cascaderNode == null) return;

            SelectedType = selectedType;
            if (selectedType == SelectedTypeEnum.Click)
            {
                _selectedNodes.Clear();
                SetSelectedNodeWithParent(cascaderNode, ref _selectedNodes);
                _renderNodes = _selectedNodes;

                if (ChangeOnSelect || !cascaderNode.HasChildren)
                    SetValue(cascaderNode.Value);
            }
            else
            {
                _hoverSelectedNodes.Clear();
                SetSelectedNodeWithParent(cascaderNode, ref _hoverSelectedNodes);
                _renderNodes = _hoverSelectedNodes;
            }
            _renderNodes.Sort((x, y) => x.Level.CompareTo(y.Level));  //Level 升序排序

            if (!cascaderNode.HasChildren)
            {
                ToggleState = false;
                IsOnCascader = false;
            }
        }

        /// <summary>
        /// 设置选中所有父节点
        /// </summary>
        /// <param name="node"></param>
        /// <param name="list"></param>
        private void SetSelectedNodeWithParent(CascaderNode node, ref List<CascaderNode> list)
        {
            if (node == null) return;

            list.Add(node);
            SetSelectedNodeWithParent(node.ParentNode, ref list);
        }

        /// <summary>
        /// Options 更新后处理父节点和默认值
        /// </summary>
        private void ProcessParentAndDefault()
        {
            InitCascaderNodeState(_nodelist, null, 0);
            SetDefaultValue(Value ?? DefaultValue);
        }

        /// <summary>
        /// 初始化节点属性(Level, ParentNode)
        /// </summary>
        /// <param name="list"></param>
        /// <param name="parentNode"></param>
        /// <param name="level"></param>
        private void InitCascaderNodeState(List<CascaderNode> list, CascaderNode parentNode, int level)
        {
            if (list == null) return;

            foreach (var node in list)
            {
                node.Level = level;
                node.ParentNode = parentNode;

                if (node.HasChildren)
                    InitCascaderNodeState(node.Children.ToList(), node, level + 1);
            }
        }

        /// <summary>
        /// 刷新选中的内容
        /// </summary>
        /// <param name="value"></param>
        private void RefreshNodeValue(string value)
        {
            _selectedNodes.Clear();

            var node = GetNodeByValue(_nodelist, value);
            SetSelectedNodeWithParent(node, ref _selectedNodes);
            _renderNodes = _selectedNodes;

            RefreshDisplayValue();

            OnChange?.Invoke(_selectedNodes, value, _displayText);
        }

        /// <summary>
        /// 设置默认选中
        /// </summary>
        /// <param name="defaultValue"></param>
        private void SetDefaultValue(string defaultValue)
        {
            if (!string.IsNullOrWhiteSpace(defaultValue))
            {
                _selectedNodes.Clear();
                var node = GetNodeByValue(_nodelist, defaultValue);
                SetSelectedNodeWithParent(node, ref _selectedNodes);
                _renderNodes = _selectedNodes;
                SetValue(node?.Value);
            }

            _initialized = true;
        }

        /// <summary>
        /// 设置输入框选中值
        /// </summary>
        /// <param name="value"></param>
        private void SetValue(string value)
        {
            RefreshDisplayValue();

            if (Value != value)
            {
                CurrentValueAsString = value;

                if (_initialized && SelectedNodesChanged.HasDelegate)
                {
                    SelectedNodesChanged.InvokeAsync(_selectedNodes.ToArray());
                }
            }
        }

        private void RefreshDisplayValue()
        {
            _selectedNodes.Sort((x, y) => x.Level.CompareTo(y.Level));  //Level 升序排序
            _displayText = string.Empty;
            int count = 0;
            foreach (var node in _selectedNodes)
            {
                if (node == null) continue;

                if (count < _selectedNodes.Count - 1)
                    _displayText += node.Label + " / ";
                else
                    _displayText += node.Label;
                count++;
            }
        }

        /// <summary>
        /// 根据指定值获取节点
        /// </summary>
        /// <param name="list"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private CascaderNode GetNodeByValue(List<CascaderNode> list, string value)
        {
            if (list == null) return null;
            CascaderNode result = null;

            foreach (var node in list)
            {
                if (node.Value == value)
                    return node;

                if (node.HasChildren)
                {
                    var nd = GetNodeByValue(node.Children.ToList(), value);
                    if (nd != null)
                        result = nd;
                }
            }
            return result;
        }
    }
}
