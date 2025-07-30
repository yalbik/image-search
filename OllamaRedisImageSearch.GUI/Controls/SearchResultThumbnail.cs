using OllamaRedisImageSearch.Models;
using System.ComponentModel;

namespace OllamaRedisImageSearch.GUI.Controls;

public partial class SearchResultThumbnail : UserControl
{
    private SearchResult _searchResult = null!;
    private Image? _thumbnail;
    private const int ThumbnailSize = 150;

    public event EventHandler<SearchResult>? ThumbnailClicked;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SearchResult SearchResult
    {
        get => _searchResult;
        set
        {
            _searchResult = value;
            LoadThumbnail();
            UpdateDisplay();
        }
    }

    public SearchResultThumbnail()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Size = new Size(180, 220);
        this.BorderStyle = BorderStyle.FixedSingle;
        this.BackColor = Color.White;
        this.Cursor = Cursors.Hand;
        this.Click += OnThumbnailClick;
        this.Paint += OnPaint;
    }

    private void LoadThumbnail()
    {
        if (_searchResult?.ImagePath == null)
            return;

        try
        {
            if (!File.Exists(_searchResult.ImagePath))
            {
                Console.WriteLine($"Image file not found: {_searchResult.ImagePath}");
                return;
            }

            using var originalImage = Image.FromFile(_searchResult.ImagePath);
            _thumbnail = CreateThumbnail(originalImage, ThumbnailSize);
        }
        catch (Exception ex)
        {
            // Handle image loading errors
            Console.WriteLine($"Error loading thumbnail for {_searchResult.ImagePath}: {ex.Message}");
        }
    }

    private Image CreateThumbnail(Image original, int size)
    {
        var aspectRatio = (float)original.Width / original.Height;
        int thumbnailWidth, thumbnailHeight;

        if (aspectRatio > 1)
        {
            thumbnailWidth = size;
            thumbnailHeight = (int)(size / aspectRatio);
        }
        else
        {
            thumbnailWidth = (int)(size * aspectRatio);
            thumbnailHeight = size;
        }

        var thumbnail = new Bitmap(thumbnailWidth, thumbnailHeight);
        using (var graphics = Graphics.FromImage(thumbnail))
        {
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(original, 0, 0, thumbnailWidth, thumbnailHeight);
        }

        return thumbnail;
    }

    private void UpdateDisplay()
    {
        this.Invalidate();
    }

    private void OnPaint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        var rect = this.ClientRectangle;

        // Draw thumbnail
        if (_thumbnail != null)
        {
            var x = (rect.Width - _thumbnail.Width) / 2;
            var y = 10;
            g.DrawImage(_thumbnail, x, y);
        }

        // Draw filename
        if (_searchResult != null)
        {
            var font = new Font("Segoe UI", 8, FontStyle.Regular);
            var brush = new SolidBrush(Color.Black);
            var textRect = new Rectangle(5, ThumbnailSize + 15, rect.Width - 10, 20);
            
            var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            };

            g.DrawString(Path.GetFileNameWithoutExtension(_searchResult.Filename), 
                        font, brush, textRect, format);

            // Draw score
            var scoreText = $"Score: {_searchResult.Score:F3}";
            var scoreRect = new Rectangle(5, ThumbnailSize + 35, rect.Width - 10, 15);
            var scoreFont = new Font("Segoe UI", 7, FontStyle.Italic);
            var scoreBrush = new SolidBrush(Color.Gray);

            g.DrawString(scoreText, scoreFont, scoreBrush, scoreRect, format);

            font.Dispose();
            brush.Dispose();
            scoreFont.Dispose();
            scoreBrush.Dispose();
        }
    }

    private void OnThumbnailClick(object? sender, EventArgs e)
    {
        if (_searchResult != null)
        {
            ThumbnailClicked?.Invoke(this, _searchResult);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _thumbnail?.Dispose();
        }
        base.Dispose(disposing);
    }
}
