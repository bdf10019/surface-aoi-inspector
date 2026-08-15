using System.Text.Json;
using System.Text.Json.Serialization;

namespace AoiInspector.Workflows;

public static class RecipeFile
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static InspectionRecipe Load(string path)
    {
        var json = File.ReadAllText(path);
        var recipe = JsonSerializer.Deserialize<InspectionRecipe>(json, Options)
            ?? throw new InvalidDataException("配方內容為空或格式無效。");
        recipe.Validate();
        return recipe;
    }

    public static void Save(string path, InspectionRecipe recipe)
    {
        recipe.Validate();
        File.WriteAllText(path, JsonSerializer.Serialize(recipe, Options));
    }
}
