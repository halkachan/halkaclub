using System.ComponentModel;

namespace HalkaSiteEditor.Core;

/// <summary>
/// 「元に戻す」1手ぶん。
/// 文字を打つのは入力欄が自分で覚えている（Ctrl+Z）ので、ここで覚えるのは
/// ボタンで起きること — 追加・並べ替え・削除・まとめて直す — だけです。
/// </summary>
public sealed record UndoStep(string Label, Action Undo);

/// <summary>押した順に覚えて、新しいものから1手ずつ戻します。</summary>
public sealed class UndoStack : INotifyPropertyChanged
{
    /// <summary>これより古い手は捨てます。</summary>
    public const int Depth = 50;

    private readonly List<UndoStep> steps = new();

    public bool CanUndo => steps.Count > 0;

    public int Count => steps.Count;

    /// <summary>次に戻す手の名前（無ければ null）。</summary>
    public string? NextLabel => steps.Count > 0 ? steps[^1].Label : null;

    public void Record(string label, Action undo)
    {
        steps.Add(new UndoStep(label, undo));
        if (steps.Count > Depth) steps.RemoveRange(0, steps.Count - Depth);
        Raise();
    }

    /// <summary>1手だけ戻します。戻した手の名前を返します（何も無ければ null）。</summary>
    public string? Undo()
    {
        if (steps.Count == 0) return null;

        var step = steps[^1];
        steps.RemoveAt(steps.Count - 1);
        step.Undo();
        Raise();
        return step.Label;
    }

    /// <summary>保存や読み込み直しのあとは、覚えていた手を捨てます。</summary>
    public void Clear()
    {
        if (steps.Count == 0) return;
        steps.Clear();
        Raise();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise()
    {
        foreach (var name in new[] { nameof(CanUndo), nameof(Count), nameof(NextLabel) })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
