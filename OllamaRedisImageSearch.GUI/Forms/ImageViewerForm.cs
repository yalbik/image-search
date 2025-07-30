using OllamaRedisImageSearch.Models;

namespace OllamaRedisImageSearch.GUI.Forms;

public partial class ImageViewerForm : Form
{
    private SearchResult _searchResult;
    private PictureBox _pictureBox = null!;
    private Label _descriptionLabel = null!;
    private Label _filenameLabel = null!;
    private Label _scoreLabel = null!;

    public ImageViewerForm(SearchResult searchResult)
    {
        _searchResult = searchResult;
        InitializeComponent();
        LoadImage();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();

        // Form properties
        this.Text = $"Image Viewer - {Path.GetFileNameWithoutExtension(_searchResult.Filename)}";
        this.Size = new Size(800, 700);
        this.StartPosition = FormStartPosition.CenterParent;
        this.MinimumSize = new Size(400, 300);

        // Filename label
        _filenameLabel = new Label
        {
            Text = _searchResult.Filename,
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(10, 10)
        };

        // Score label
        _scoreLabel = new Label
        {
            Text = $"Similarity Score: {_searchResult.Score:F3}",
            Font = new Font("Segoe UI", 10, FontStyle.Regular),
            ForeColor = Color.Gray,
            AutoSize = true,
            Location = new Point(10, 35)
        };

        // Picture box
        _pictureBox = new PictureBox
        {
            Location = new Point(10, 60),
            Size = new Size(760, 400),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            SizeMode = PictureBoxSizeMode.Zoom,
            BorderStyle = BorderStyle.FixedSingle
        };

        // Description label
        _descriptionLabel = new Label
        {
            Text = "Description: " + _searchResult.Description,
            Font = new Font("Segoe UI", 9, FontStyle.Regular),
            Location = new Point(10, 470),
            Size = new Size(760, 180),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            AutoEllipsis = true,
            BackColor = Color.LightGray,
            Padding = new Padding(5),
            BorderStyle = BorderStyle.FixedSingle
        };

        // Add controls
        this.Controls.Add(_filenameLabel);
        this.Controls.Add(_scoreLabel);
        this.Controls.Add(_pictureBox);
        this.Controls.Add(_descriptionLabel);

        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private void LoadImage()
    {
        try
        {
            if (File.Exists(_searchResult.ImagePath))
            {
                _pictureBox.Image = Image.FromFile(_searchResult.ImagePath);
            }
            else
            {
                _pictureBox.Image = null;
                MessageBox.Show($"Image file not found: {_searchResult.ImagePath}", 
                               "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading image: {ex.Message}", 
                           "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pictureBox?.Image?.Dispose();
        }
        base.Dispose(disposing);
    }
}
