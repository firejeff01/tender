using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Tender.Desktop.Helpers;

/// <summary>
/// 為 ListBox 加上 drag-and-drop 重新排序能力。
/// 用法：<c>ListBoxReorderHelper.Attach(myListBox, (from, to) =&gt; vm.Move(from, to));</c>
/// 真正的集合操作交給 callback；helper 不假設 ItemsSource 是哪種集合，只算 index。
/// </summary>
public static class ListBoxReorderHelper
{
    private const double DragThreshold = 6.0;

    private sealed class State
    {
        public ListBox ListBox = null!;
        public Action<int, int> OnReorder = null!;
        public Point StartPoint;
        public int DragStartIndex = -1;
    }

    public static void Attach(ListBox listBox, Action<int, int> onReorder)
    {
        var state = new State { ListBox = listBox, OnReorder = onReorder };
        listBox.AllowDrop = true;
        listBox.PreviewMouseLeftButtonDown += (s, e) => OnMouseDown(state, e);
        listBox.PreviewMouseMove += (s, e) => OnMouseMove(state, e);
        listBox.Drop += (s, e) => OnDrop(state, e);
        listBox.DragOver += (s, e) =>
        {
            e.Effects = e.Data.GetDataPresent(typeof(int)) ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        };
    }

    private static void OnMouseDown(State state, MouseButtonEventArgs e)
    {
        // 點擊時記錄起始 index；交給 ListBox 本身先處理 selection，等 MouseMove 才啟動 drag
        var item = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (item is null) return;
        state.StartPoint = e.GetPosition(state.ListBox);
        state.DragStartIndex = state.ListBox.ItemContainerGenerator.IndexFromContainer(item);
    }

    private static void OnMouseMove(State state, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || state.DragStartIndex < 0) return;
        var pos = e.GetPosition(state.ListBox);
        if (Math.Abs(pos.X - state.StartPoint.X) < DragThreshold &&
            Math.Abs(pos.Y - state.StartPoint.Y) < DragThreshold) return;

        // 啟動 drag-drop；payload 放 source index
        try
        {
            DragDrop.DoDragDrop(state.ListBox, state.DragStartIndex, DragDropEffects.Move);
        }
        finally
        {
            state.DragStartIndex = -1;
        }
    }

    private static void OnDrop(State state, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(int))) return;
        var from = (int)e.Data.GetData(typeof(int));

        // 找到 drop 目標的 index：游標下方的 ListBoxItem；若 drop 到空白處則放最末
        var targetItem = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        int to;
        if (targetItem is not null)
        {
            to = state.ListBox.ItemContainerGenerator.IndexFromContainer(targetItem);
        }
        else
        {
            to = state.ListBox.Items.Count - 1;
        }
        if (to < 0 || from == to) return;
        state.OnReorder(from, to);
        e.Handled = true;
    }

    private static T? FindAncestor<T>(DependencyObject? d) where T : DependencyObject
    {
        while (d != null)
        {
            if (d is T t) return t;
            d = VisualTreeHelper.GetParent(d);
        }
        return null;
    }
}
