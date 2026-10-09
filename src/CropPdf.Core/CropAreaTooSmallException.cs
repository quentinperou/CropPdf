namespace CropPdf.Core;

/// <summary>
/// The selected area is smaller than <see cref="PdfCropper.MinimumSize"/> on one side.
/// A dedicated type lets the UI show its own (localized) message.
/// </summary>
public sealed class CropAreaTooSmallException()
    : InvalidOperationException("The selected area is too small to be cropped.");