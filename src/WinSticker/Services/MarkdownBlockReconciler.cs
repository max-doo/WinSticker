namespace WinSticker.Services;

/// <summary>A block whose rendered view can be carried over from the old
/// block list to position <paramref name="NewIndex"/> unchanged.</summary>
public readonly record struct MarkdownBlockReuse(int OldIndex, int NewIndex);

/// <summary>
/// Reconciliation plan between two block lists. The changed range
/// (<see cref="FirstChangedNewIndex"/>..<see cref="LastChangedNewIndex"/>)
/// covers every new block that is not part of a reuse and is empty (-1/-1)
/// when the change is a pure removal.
/// </summary>
public sealed record MarkdownBlockPlan(
    IReadOnlyList<MarkdownBlockReuse> Reuses,
    int FirstChangedNewIndex,
    int LastChangedNewIndex,
    bool Unchanged);

/// <summary>
/// Plans block-level UI updates for the live Markdown editor: blocks whose
/// source is Ordinal-equal keep their existing rendered view, and only the
/// changed middle region is rebuilt. Matching is prefix/suffix common runs
/// (not LCS) because real edits — editing one block, splitting one block
/// into several, merging several back — always land inside a single middle
/// region, while LCS would add quadratic cost and ambiguous semantics for
/// duplicate-content blocks.
/// </summary>
public static class MarkdownBlockReconciler
{
    /// <summary>
    /// Above this block count the reconciler gives up on reuse and returns
    /// a full-replace plan; a defensive bound so pathological documents
    /// cannot make planning itself a cost.
    /// </summary>
    public const int MaxBlocksForReconcile = 2000;

    public static MarkdownBlockPlan Plan(
        IReadOnlyList<string> oldBlocks,
        IReadOnlyList<string> newBlocks)
    {
        if (oldBlocks.Count > MaxBlocksForReconcile ||
            newBlocks.Count > MaxBlocksForReconcile)
        {
            return FullReplace(newBlocks.Count);
        }

        int limit = Math.Min(oldBlocks.Count, newBlocks.Count);
        int prefix = 0;
        while (prefix < limit &&
               string.Equals(oldBlocks[prefix], newBlocks[prefix], StringComparison.Ordinal))
        {
            prefix++;
        }

        int suffix = 0;
        while (suffix < limit - prefix &&
               string.Equals(
                   oldBlocks[oldBlocks.Count - 1 - suffix],
                   newBlocks[newBlocks.Count - 1 - suffix],
                   StringComparison.Ordinal))
        {
            suffix++;
        }

        if (prefix == oldBlocks.Count && prefix == newBlocks.Count)
        {
            var identityReuses = new MarkdownBlockReuse[oldBlocks.Count];
            for (int index = 0; index < identityReuses.Length; index++)
            {
                identityReuses[index] = new MarkdownBlockReuse(index, index);
            }

            return new MarkdownBlockPlan(
                identityReuses,
                -1,
                -1,
                Unchanged: true);
        }

        var reuses = new List<MarkdownBlockReuse>(prefix + suffix);
        for (int index = 0; index < prefix; index++)
        {
            reuses.Add(new MarkdownBlockReuse(index, index));
        }

        for (int offset = suffix - 1; offset >= 0; offset--)
        {
            reuses.Add(new MarkdownBlockReuse(
                oldBlocks.Count - 1 - offset,
                newBlocks.Count - 1 - offset));
        }

        int changedStart = prefix;
        int changedEnd = newBlocks.Count - suffix - 1;
        return new MarkdownBlockPlan(
            reuses,
            changedStart <= changedEnd ? changedStart : -1,
            changedStart <= changedEnd ? changedEnd : -1,
            Unchanged: false);
    }

    private static MarkdownBlockPlan FullReplace(int newBlockCount) =>
        new(
            Array.Empty<MarkdownBlockReuse>(),
            FirstChangedNewIndex: newBlockCount > 0 ? 0 : -1,
            LastChangedNewIndex: newBlockCount > 0 ? newBlockCount - 1 : -1,
            Unchanged: false);
}
