using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Wpf.Ui.Controls;

namespace SmartDocScan.Desktop.Views;

public partial class ScanPreviewWindow : FluentWindow
{
    private readonly string _imagePath;
    private int _rotationAngle = 0;
    private BitmapImage? _originalBitmap;

    public ScanPreviewWindow(string imagePath)
    {
        InitializeComponent();
        _imagePath = imagePath;

        LoadPreviewImage();
    }

    private void LoadPreviewImage()
    {
        if (File.Exists(_imagePath))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(_imagePath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                _originalBitmap = bitmap;
                PreviewImage.Source = _originalBitmap;

                PageInfoTextBlock.Text = $"Page 1 of 1 | Width: {bitmap.PixelWidth}px, Height: {bitmap.PixelHeight}px | NAPS2 Engine";
            }
            catch (Exception ex)
            {
                PageInfoTextBlock.Text = $"Error loading preview: {ex.Message}";
            }
        }
    }

    private void OnRotateLeftClicked(object sender, RoutedEventArgs e)
    {
        _rotationAngle = (_rotationAngle - 90 + 360) % 360;
        ApplyRotation();
    }

    private void OnRotateRightClicked(object sender, RoutedEventArgs e)
    {
        _rotationAngle = (_rotationAngle + 90) % 360;
        ApplyRotation();
    }

    private void ApplyRotation()
    {
        if (_originalBitmap == null) return;

        if (_rotationAngle == 0)
        {
            PreviewImage.Source = _originalBitmap;
        }
        else
        {
            var rotation = _rotationAngle switch
            {
                90 => Rotation.Rotate90,
                180 => Rotation.Rotate180,
                270 => Rotation.Rotate270,
                _ => Rotation.Rotate0
            };

            var transformed = new TransformedBitmap();
            transformed.BeginInit();
            transformed.Source = _originalBitmap;
            transformed.Transform = new System.Windows.Media.RotateTransform(_rotationAngle);
            transformed.EndInit();
            transformed.Freeze();

            PreviewImage.Source = transformed;
        }
    }

    private void OnUploadClicked(object sender, RoutedEventArgs e)
    {
        SaveRotatedImageIfNeeded();
        DialogResult = true;
        Close();
    }

    private void OnDiscardClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void SaveRotatedImageIfNeeded()
    {
        if (_rotationAngle != 0 && PreviewImage.Source is BitmapSource bitmapSource)
        {
            try
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                using var stream = File.Create(_imagePath);
                encoder.Save(stream);
            }
            catch { }
        }
    }
}
