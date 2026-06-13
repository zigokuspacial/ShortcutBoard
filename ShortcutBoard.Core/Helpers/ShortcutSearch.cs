using ShortcutBoard.Models;

namespace ShortcutBoard.Helpers;

/// <summary>
/// 検索・カテゴリー絞り込みの共通ロジック。（Windows/macOS 共通）
/// </summary>
public static class ShortcutSearch
{
    public const string AllCategories = "すべて";

    /// <summary>
    /// 指定のキーワード・カテゴリーに一致するか判定する。
    /// </summary>
    public static bool Matches(Shortcut s, string? query, string? category)
    {
        if (!string.IsNullOrEmpty(category) && category != AllCategories
            && s.DisplayCategory != category)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(query))
            return true;

        var q = query.Trim();
        return Contains(s.Keys, q) || Contains(s.Name, q)
            || Contains(s.Description, q) || Contains(s.Category, q);
    }

    /// <summary>一覧から重複なしのカテゴリー一覧（先頭に「すべて」）を作る。</summary>
    public static List<string> BuildCategoryList(IEnumerable<Shortcut> items)
    {
        var result = new List<string> { AllCategories };
        result.AddRange(items.Select(s => s.DisplayCategory).Distinct().OrderBy(c => c));
        return result;
    }

    private static bool Contains(string? value, string q)
        => value is not null && value.Contains(q, StringComparison.OrdinalIgnoreCase);
}
