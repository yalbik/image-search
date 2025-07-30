using OllamaRedisImageSearch.Models;

namespace OllamaRedisImageSearch.GUI.Forms;

public partial class ImageViewerForm : Form
{
    private SearchResult _searchResult;
    private string? _relevancyInfo;
    private PictureBox _pictureBox = null!;
    private RichTextBox _descriptionTextBox = null!;
    private Label _filenameLabel = null!;
    private Label _scoreLabel = null!;

    public ImageViewerForm(SearchResult searchResult, string? relevancyInfo = null)
    {
        _searchResult = searchResult;
        _relevancyInfo = relevancyInfo;
        InitializeComponent();
        LoadImage();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();

        // Form properties
        this.Text = $"Image Viewer - {Path.GetFileNameWithoutExtension(_searchResult.Filename)}";
        this.Size = new Size(900, 800);
        this.StartPosition = FormStartPosition.CenterParent;
        this.MinimumSize = new Size(600, 500);

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
            Size = new Size(860, 400),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            SizeMode = PictureBoxSizeMode.Zoom,
            BorderStyle = BorderStyle.FixedSingle
        };

        // Description text box (scrollable)
        _descriptionTextBox = new RichTextBox
        {
            Location = new Point(10, 470),
            Size = new Size(860, 280),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
            ReadOnly = true,
            Font = new Font("Segoe UI", 9, FontStyle.Regular),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            ScrollBars = RichTextBoxScrollBars.Vertical
        };

        // Set the description text with relevancy information
        SetDescriptionText();

        // Add controls
        this.Controls.Add(_filenameLabel);
        this.Controls.Add(_scoreLabel);
        this.Controls.Add(_pictureBox);
        this.Controls.Add(_descriptionTextBox);

        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private void SetDescriptionText()
    {
        _descriptionTextBox.Clear();
        
        // Add description section
        _descriptionTextBox.SelectionFont = new Font("Segoe UI", 10, FontStyle.Bold);
        _descriptionTextBox.SelectionColor = Color.DarkBlue;
        _descriptionTextBox.AppendText("Image Description:\n");
        
        _descriptionTextBox.SelectionFont = new Font("Segoe UI", 9, FontStyle.Regular);
        _descriptionTextBox.SelectionColor = Color.Black;
        _descriptionTextBox.AppendText(_searchResult.Description + "\n\n");
        
        // Add relevancy information if available
        if (!string.IsNullOrEmpty(_relevancyInfo))
        {
            _descriptionTextBox.SelectionFont = new Font("Segoe UI", 10, FontStyle.Bold);
            _descriptionTextBox.SelectionColor = Color.DarkGreen;
            _descriptionTextBox.AppendText("Relevancy Analysis:\n");
            
            _descriptionTextBox.SelectionFont = new Font("Segoe UI", 9, FontStyle.Regular);
            _descriptionTextBox.SelectionColor = Color.Black;
            _descriptionTextBox.AppendText(_relevancyInfo);
        }
        
        // Reset selection to start
        _descriptionTextBox.SelectionStart = 0;
        _descriptionTextBox.SelectionLength = 0;
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
