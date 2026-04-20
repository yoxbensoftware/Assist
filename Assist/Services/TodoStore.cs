namespace Assist.Services;

using System.Text.Json;
using Assist.Models;

/// <summary>
/// Persistent JSON-backed store for to-do items.
/// </summary>
internal static class TodoStore
{
    private static readonly string FilePath =
        Path.Combine(AppConstants.AppDataPath, "todos.json");

    private static List<TodoItem> _items = [];

    public static IReadOnlyList<TodoItem> Items => _items.AsReadOnly();

    public static void Load()
    {
        EnsureDir();
        if (!File.Exists(FilePath)) { _items = []; return; }
        try
        {
            var json = File.ReadAllText(FilePath);
            _items = JsonSerializer.Deserialize<List<TodoItem>>(json) ?? [];
        }
        catch { _items = []; }

        // Periyodik görevlerde geçmiş kalmış tarihleri otomatik ilerlet
        bool changed = false;
        foreach (var item in _items.Where(x => x.IsRecurring && !x.IsCompleted && x.DueDate.HasValue))
        {
            while (item.DueDate!.Value.Date < DateTime.Today)
            {
                item.DueDate = item.NextOccurrenceAfter(item.DueDate.Value);
                changed = true;
            }
        }
        if (changed) Save();
    }

    public static void Save()
    {
        EnsureDir();
        File.WriteAllText(FilePath, JsonSerializer.Serialize(
            _items, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static void Add(TodoItem item)
    {
        _items.Add(item);
        Save();
    }

    public static void Update(TodoItem item)
    {
        var idx = _items.FindIndex(x => x.Id == item.Id);
        if (idx >= 0) { _items[idx] = item; Save(); }
    }

    public static void Delete(Guid id)
    {
        _items.RemoveAll(x => x.Id == id);
        Save();
    }

    public static void ToggleComplete(Guid id)
    {
        var item = _items.FirstOrDefault(x => x.Id == id);
        if (item is null) return;

        if (item.IsRecurring)
        {
            // Periyodik görevler asla "bitti" olarak işaretlenmez;
            // tamamlandığında DueDate bir sonraki tekrara atlar.
            item.DueDate = item.NextOccurrenceAfter(item.DueDate ?? DateTime.Today);
        }
        else
        {
            item.IsCompleted = !item.IsCompleted;
            item.CompletedAt = item.IsCompleted ? DateTime.Now : null;
        }
        Save();
    }

    private static void EnsureDir() => Directory.CreateDirectory(AppConstants.AppDataPath);
}
