namespace CropPdf.Core;

public static class OutputPath
{
    public const string CropSuffix = "_crop";

    /// <summary>
    /// "C:\docs\plan.pdf" gives "C:\docs\plan_crop.pdf", or "plan_crop_2.pdf", "plan_crop_3.pdf"…
    /// when that name is already taken, so an existing file is never overwritten.
    /// </summary>
    public static string GetCropCopyPath(string sourcePath)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(sourcePath))!;
        string name = Path.GetFileNameWithoutExtension(sourcePath);
        string extension = Path.GetExtension(sourcePath);

        string candidate = Path.Combine(directory, name + CropSuffix + extension);
        for (int i = 2; File.Exists(candidate); i++)
            candidate = Path.Combine(directory, $"{name}{CropSuffix}_{i}{extension}");
        return candidate;
    }
}
