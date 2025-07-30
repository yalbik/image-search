using OllamaRedisImageSearch.Config;
using OllamaRedisImageSearch.Services;
using OllamaRedisImageSearch.Models;
using OllamaRedisImageSearch.GUI.Controls;
using OllamaRedisImageSearch.GUI.Forms;

namespace OllamaRedisImageSearch.GUI;

public partial class Form1 : Form
{
    private AppConfig _config;
    private IOllamaService? _ollamaService;
    private IRedisVectorService? _redisService;
    private IImageSearchService? _imageSearchService;
    
    private TextBox _searchTextBox = null!;
    private Button _searchButton = null!;
    private Button _indexButton = null!;
    private FlowLayoutPanel _resultsPanel = null!;
    private Label _statusLabel = null!;
    private ProgressBar _progressBar = null!;
    private RichTextBox _summaryTextBox = null!;
    private Label _metricsLabel = null!;

    public Form1()
    {
        _config = new AppConfig();
        InitializeComponent();
        InitializeServicesAsync();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();

        // Form properties
        this.Text = "MCP Image Fun App - Natural Language Image Search";
        this.Size = new Size(1200, 800);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.MinimumSize = new Size(800, 600);

        // Search textbox
        _searchTextBox = new TextBox
        {
            Location = new Point(10, 15),
            Size = new Size(400, 25),
            PlaceholderText = "Enter search query (e.g., 'beautiful woman', 'disc golf')",
            Font = new Font("Segoe UI", 10)
        };

        // Search button
        _searchButton = new Button
        {
            Text = "Search Images",
            Location = new Point(420, 12),
            Size = new Size(100, 30),
            Enabled = false
        };
        _searchButton.Click += SearchButton_Click;

        // Index button
        _indexButton = new Button
        {
            Text = "Index Images",
            Location = new Point(530, 12),
            Size = new Size(100, 30),
            Enabled = false
        };
        _indexButton.Click += IndexButton_Click;

        // Status label
        _statusLabel = new Label
        {
            Text = "Initializing services...",
            Location = new Point(10, 50),
            Size = new Size(600, 20),
            Font = new Font("Segoe UI", 9)
        };

        // Progress bar
        _progressBar = new ProgressBar
        {
            Location = new Point(10, 75),
            Size = new Size(600, 20),
            Style = ProgressBarStyle.Marquee,
            Visible = false
        };

        // Results panel
        _resultsPanel = new FlowLayoutPanel
        {
            Location = new Point(10, 105),
            Size = new Size(580, 650),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom,
            AutoScroll = true,
            BorderStyle = BorderStyle.FixedSingle,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true
        };

        // Summary text box
        _summaryTextBox = new RichTextBox
        {
            Location = new Point(600, 105),
            Size = new Size(580, 500),
            Anchor = AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom,
            ReadOnly = true,
            Font = new Font("Segoe UI", 9),
            Text = "AI-generated summary will appear here after searching..."
        };

        // Metrics label
        _metricsLabel = new Label
        {
            Location = new Point(600, 615),
            Size = new Size(580, 140),
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
            Font = new Font("Consolas", 8),
            Text = "Performance metrics will appear here..."
        };

        // Add controls to form
        this.Controls.Add(_searchTextBox);
        this.Controls.Add(_searchButton);
        this.Controls.Add(_indexButton);
        this.Controls.Add(_statusLabel);
        this.Controls.Add(_progressBar);
        this.Controls.Add(_resultsPanel);
        this.Controls.Add(_summaryTextBox);
        this.Controls.Add(_metricsLabel);

        // Enable Enter key for search
        _searchTextBox.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter && _searchButton.Enabled)
            {
                SearchButton_Click(_searchButton, EventArgs.Empty);
            }
        };

        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private async void InitializeServicesAsync()
    {
        try
        {
            _statusLabel.Text = "Checking Redis connectivity...";
            if (!await CheckRedisConnectivityAsync())
            {
                _statusLabel.Text = "❌ Redis connection failed. Please start Redis Stack.";
                return;
            }

            _statusLabel.Text = "Checking Ollama connectivity...";
            var ollamaCheck = await CheckOllamaConnectivityAsync();
            if (!ollamaCheck.isConnected)
            {
                _statusLabel.Text = "❌ Ollama connection failed. Please start Ollama service.";
                return;
            }

            if (!ollamaCheck.hasRequiredModels)
            {
                _statusLabel.Text = "❌ Required Ollama models missing. Check console for details.";
                return;
            }

            _statusLabel.Text = "Initializing services...";
            _ollamaService = new OllamaService(_config);
            _redisService = new RedisVectorService(_config);
            _imageSearchService = new ImageSearchService(_ollamaService, _redisService, _config);

            if (await _imageSearchService.InitializeAsync())
            {
                _statusLabel.Text = "✅ Ready! Enter a search query or index images first.";
                _searchButton.Enabled = true;
                _indexButton.Enabled = true;
            }
            else
            {
                _statusLabel.Text = "❌ Failed to initialize image search service.";
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"❌ Initialization error: {ex.Message}";
        }
    }

    private async Task<bool> CheckRedisConnectivityAsync()
    {
        // Simplified Redis check - you could implement the full check from Program.cs
        try
        {
            using var redisService = new RedisVectorService(_config);
            return await redisService.EnsureRedisIndexExistsAsync();
        }
        catch
        {
            return false;
        }
    }

    private async Task<(bool isConnected, bool hasRequiredModels)> CheckOllamaConnectivityAsync()
    {
        // Simplified Ollama check - you could implement the full check from Program.cs
        using var client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(5);

        try
        {
            var response = await client.GetAsync($"{_config.OllamaBaseUrl}/api/tags");
            return (response.IsSuccessStatusCode, true); // Simplified for demo
        }
        catch
        {
            return (false, false);
        }
    }

    private async void SearchButton_Click(object? sender, EventArgs e)
    {
        var query = _searchTextBox.Text.Trim();
        if (string.IsNullOrEmpty(query))
        {
            MessageBox.Show("Please enter a search query.", "Search Query Required", 
                           MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_imageSearchService == null)
        {
            MessageBox.Show("Services not initialized.", "Error", 
                           MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        try
        {
            _searchButton.Enabled = false;
            _progressBar.Visible = true;
            _statusLabel.Text = "Searching...";
            _resultsPanel.Controls.Clear();
            _summaryTextBox.Text = "Searching... This may take a moment.";

            var searchResult = await _imageSearchService.SearchWithSummaryAsync(query);
            
            if (searchResult.Results.Count == 0)
            {
                _statusLabel.Text = "No matching images found.";
                _summaryTextBox.Text = searchResult.Summary;
            }
            else
            {
                _statusLabel.Text = $"Found {searchResult.Results.Count} matching images.";
                DisplaySearchResults(searchResult.Results);
                _summaryTextBox.Text = searchResult.Summary;
                DisplayMetrics(searchResult.Metrics);
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Search error: {ex.Message}";
            MessageBox.Show($"Search failed: {ex.Message}", "Search Error", 
                           MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _searchButton.Enabled = true;
            _progressBar.Visible = false;
        }
    }

    private async void IndexButton_Click(object? sender, EventArgs e)
    {
        if (_imageSearchService == null)
        {
            MessageBox.Show("Services not initialized.", "Error", 
                           MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var result = MessageBox.Show(
            $"This will index all images in the '{_config.ImagesPath}' folder. This may take several minutes. Continue?",
            "Index Images", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (result != DialogResult.Yes)
            return;

        try
        {
            _indexButton.Enabled = false;
            _searchButton.Enabled = false;
            _progressBar.Visible = true;
            _statusLabel.Text = "Indexing images...";

            var success = await _imageSearchService.IndexImagesAsync(_config.ImagesPath);
            
            if (success)
            {
                _statusLabel.Text = "✅ Image indexing completed successfully!";
                MessageBox.Show("Image indexing completed successfully!", "Indexing Complete", 
                               MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                _statusLabel.Text = "❌ Image indexing failed.";
                MessageBox.Show("Image indexing failed. Check the console for details.", "Indexing Failed", 
                               MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Indexing error: {ex.Message}";
            MessageBox.Show($"Indexing failed: {ex.Message}", "Indexing Error", 
                           MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _indexButton.Enabled = true;
            _searchButton.Enabled = true;
            _progressBar.Visible = false;
        }
    }

    private void DisplaySearchResults(List<SearchResult> results)
    {
        _resultsPanel.Controls.Clear();

        foreach (var result in results)
        {
            var thumbnail = new SearchResultThumbnail
            {
                SearchResult = result
            };
            
            thumbnail.ThumbnailClicked += (s, searchResult) =>
            {
                var viewer = new ImageViewerForm(searchResult);
                viewer.ShowDialog(this);
            };

            _resultsPanel.Controls.Add(thumbnail);
        }
    }

    private void DisplayMetrics(McpMetrics metrics)
    {
        _metricsLabel.Text = $@"=== Performance Metrics ===
Embedding Time: {metrics.EmbeddingTime.TotalMilliseconds:F0}ms
Search Time: {metrics.SearchTime.TotalMilliseconds:F0}ms
Summarization Time: {metrics.SummarizationTime.TotalMilliseconds:F0}ms
Total Time: {(metrics.EmbeddingTime + metrics.SearchTime + metrics.SummarizationTime).TotalMilliseconds:F0}ms
Results Processed: {metrics.ResultsProcessed}
Estimated Tokens Used: {metrics.TokensUsed}";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _imageSearchService?.Dispose();
            _ollamaService?.Dispose();
            _redisService?.Dispose();
        }
        base.Dispose(disposing);
    }
}
