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
    private SearchResultWithSummary? _currentSearchResult;
    
    private TextBox _searchTextBox = null!;
    private Button _searchButton = null!;
    private Button _indexButton = null!;
    private ComboBox _modelComboBox = null!;
    private Label _modelLabel = null!;
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

        // Model selection label
        _modelLabel = new Label
        {
            Text = "Summarization Model:",
            Location = new Point(650, 15),
            Size = new Size(150, 20),
            Font = new Font("Segoe UI", 9)
        };

        // Model selection combo box
        _modelComboBox = new ComboBox
        {
            Location = new Point(810, 12),
            Size = new Size(200, 25),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9),
            Enabled = false
        };
        _modelComboBox.SelectedIndexChanged += ModelComboBox_SelectedIndexChanged;

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
        this.Controls.Add(_modelLabel);
        this.Controls.Add(_modelComboBox);
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

            // Flush VRAM at startup to ensure clean GPU state
            if (_config.AutoFlushVramOnModelSwitch)
            {
                _statusLabel.Text = "Flushing GPU memory at startup...";
                try
                {
                    await _ollamaService.UnloadAllModelsAsync();
                    Console.WriteLine("VRAM flushed at application startup");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not flush VRAM at startup: {ex.Message}");
                }
            }

            if (await _imageSearchService.InitializeAsync())
            {
                _statusLabel.Text = "✅ Ready! Loading available models...";
                _searchButton.Enabled = true;
                _indexButton.Enabled = true;
                
                // Load available models after successful initialization
                await LoadAvailableModelsAsync();
                _statusLabel.Text = "✅ Ready! Enter a search query or index images first.";
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

    private async Task LoadAvailableModelsAsync()
    {
        if (_ollamaService == null) return;

        try
        {
            var models = await _ollamaService.GetAvailableModelsAsync();
            _modelComboBox.Items.Clear();
            
            foreach (var model in models)
            {
                _modelComboBox.Items.Add(model);
            }

            // Set current selection to the configured summarization model
            var currentModel = _config.SummarizationModel;
            var index = _modelComboBox.Items.IndexOf(currentModel);
            if (index >= 0)
            {
                _modelComboBox.SelectedIndex = index;
            }
            else if (_modelComboBox.Items.Count > 0)
            {
                _modelComboBox.SelectedIndex = 0;
            }

            _modelComboBox.Enabled = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading models: {ex.Message}");
            _modelComboBox.Items.Add(_config.SummarizationModel);
            _modelComboBox.SelectedIndex = 0;
            _modelComboBox.Enabled = false;
        }
    }

    private async void ModelComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_modelComboBox.SelectedItem != null && _ollamaService != null)
        {
            var oldModel = _config.SummarizationModel;
            var newModel = _modelComboBox.SelectedItem.ToString() ?? _config.SummarizationModel;
            
            if (oldModel != newModel)
            {
                _config.SummarizationModel = newModel;
                
                // Auto-flush VRAM if enabled in config
                if (_config.AutoFlushVramOnModelSwitch)
                {
                    _statusLabel.Text = "Flushing GPU memory and switching model...";
                    
                    try
                    {
                        await _ollamaService.UnloadAllModelsAsync();
                        Console.WriteLine($"VRAM flushed. Summarization model changed from {oldModel} to {newModel}");
                        _statusLabel.Text = "✅ Ready! GPU memory flushed and model switched.";
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error flushing VRAM during model switch: {ex.Message}");
                        _statusLabel.Text = "⚠️ Model switched but VRAM flush failed.";
                    }
                }
                else
                {
                    Console.WriteLine($"Summarization model changed from {oldModel} to {newModel}");
                    _statusLabel.Text = "✅ Ready! Model switched.";
                }
            }
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
            _currentSearchResult = searchResult;
            
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
                var relevancyInfo = ExtractRelevancyInfo(searchResult);
                var viewer = new ImageViewerForm(searchResult, relevancyInfo);
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

    private string? ExtractRelevancyInfo(SearchResult searchResult)
    {
        if (_currentSearchResult?.Summary == null)
            return null;

        var summary = _currentSearchResult.Summary;
        var filename = searchResult.Filename;
        
        // Try to extract information about this specific image from the summary
        // Look for the filename or partial filename in the summary
        var lines = summary.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var relevantLines = new List<string>();
        
        foreach (var line in lines)
        {
            // Check if the line mentions this specific image
            if (line.Contains(filename, StringComparison.OrdinalIgnoreCase) ||
                line.Contains(Path.GetFileNameWithoutExtension(filename), StringComparison.OrdinalIgnoreCase))
            {
                relevantLines.Add(line.Trim());
            }
        }
        
        if (relevantLines.Count > 0)
        {
            return string.Join("\n", relevantLines);
        }
        
        // If no specific mention, return a generic relevancy based on similarity score
        return $"This image has a similarity score of {searchResult.Score:F3}, indicating it's a {GetRelevancyDescription(searchResult.Score)} match for your search query.";
    }
    
    private string GetRelevancyDescription(float score)
    {
        return score switch
        {
            >= 0.8f => "very strong",
            >= 0.6f => "strong",
            >= 0.4f => "moderate",
            >= 0.2f => "weak",
            _ => "minimal"
        };
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
