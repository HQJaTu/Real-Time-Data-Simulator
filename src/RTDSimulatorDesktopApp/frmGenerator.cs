using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using RTDSimulatorDesktopApp.FormControls;
using System.ComponentModel;
using System.Text;
using System.Data;
using Azure.Core;
using Azure.Identity;
using System.Net;
using RTDSimulator.Core;
using RTDSimulator.EventHubs;
using RTDSimulator.ServiceBus;
using RTDSimulator.Kusto;

namespace RTDSimulatorDesktopApp;

public partial class frmGenerator : Form
{
    public frmGenerator()
    {
        InitializeComponent();
    }

    private const string FILES_FILTER = "Text files (*.txt)|*.txt|JSON files (*.json)|*.json|All files (*.*)|*.*";
    private const Double BytesToMbps = 1024 * 1024 / 8;

    private string _FileName = "GenerationByFuelType"; // default save target
    private bool _hasNamedFile = false;                // true once a template is opened/saved
    private bool _IsPayloadChanged = false;
    private decimal _TotalMsgCount = 0;
    private decimal _TotalBatchCount = 0;
    private int _BatchSent = 0;
    private int _MsgSent = 0;
    private Int64 _TotalSizeInBytes = 0;
    private DateTime _StartTime = DateTime.MinValue;
    private DateTime _EndTime = DateTime.MinValue;
    private CancellationTokenSource _CancellationTokenSource;
    private ITargetConnection _conn;
    private InteractiveBrowserCredential _credential;
    private string? _signedInUser;

    // Persisted, encrypted token cache + authentication record so the interactive
    // sign-in survives across app runs (silent re-auth until the refresh token expires).
    private const string TokenCacheName = "RealTimeDataSimulator";
    private static readonly string AuthCacheDir = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RealTimeDataSimulator");
    private static readonly string AuthRecordPath = System.IO.Path.Combine(AuthCacheDir, "auth.record");

    private static InteractiveBrowserCredential CreatePersistentCredential(AuthenticationRecord? record)
    {
        var options = new InteractiveBrowserCredentialOptions
        {
            TokenCachePersistenceOptions = new TokenCachePersistenceOptions { Name = TokenCacheName },
            AuthenticationRecord = record,
        };
        return new InteractiveBrowserCredential(options);
    }

    // Restores a previously cached sign-in (if any) so the user doesn't have to log in again.
    private void TryRestoreCachedSignIn()
    {
        if (!System.IO.File.Exists(AuthRecordPath))
            return;
        try
        {
            using var stream = System.IO.File.OpenRead(AuthRecordPath);
            AuthenticationRecord record = AuthenticationRecord.Deserialize(stream);
            _credential = CreatePersistentCredential(record);
            _signedInUser = record.Username;
        }
        catch (Exception ex)
        {
            lastErrorTextBox.Text = $"Could not restore cached sign-in: {ex.Message}";
        }
    }

    // Reflects sign-in state in the auth button (and its tooltip), which persists
    // regardless of other status messages.
    private void UpdateAuthUi()
    {
        bool signedIn = _credential != null;
        btnAzureAuth.Text = signedIn ? "Azure: Sign out" : "Azure: Sign in";
        toolTip1.SetToolTip(btnAzureAuth, signedIn
            ? $"Signed in as {_signedInUser} — click to sign out"
            : "Sign in with Microsoft Entra ID");
    }

    /// <summary>Soft-coded variable definitions used to expand the payload template.</summary>
    private IReadOnlyList<VariableDefinition> _variables = Array.Empty<VariableDefinition>();

    /// <summary>Connectors available for selection. Add new connectors here.</summary>
    private readonly IReadOnlyList<ITargetConnector> _connectors = new ITargetConnector[]
    {
        new EventHubsConnector(),
        new ServiceBusConnector(),
        new KustoStreamingConnector(),
        new KustoQueuedConnector(),
    };

    /// <summary>Live parameter text boxes for the selected connector, keyed by parameter key.</summary>
    private readonly Dictionary<string, System.Windows.Forms.TextBox> _paramInputs = new();

    private ITargetConnector SelectedConnector => _connectors[Math.Max(0, cboTargetType.SelectedIndex)];

    /// <summary>Rebuilds the parameter input rows to match the selected connector.</summary>
    private void RebuildParameterInputs()
    {
        pnlParams.SuspendLayout();
        pnlParams.Controls.Clear();
        pnlParams.RowStyles.Clear();
        pnlParams.RowCount = 0;
        _paramInputs.Clear();

        foreach (ConnectionParameter p in SelectedConnector.Parameters)
        {
            var label = new Label
            {
                Text = p.Label + ":",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(3, 5, 3, 0),
            };

            var input = new System.Windows.Forms.TextBox
            {
                Text = p.DefaultValue ?? string.Empty,
                Dock = DockStyle.Fill,
                UseSystemPasswordChar = p.Secret,
                Margin = new Padding(3, 2, 3, 2),
            };
            if (!string.IsNullOrEmpty(p.HelpText))
            {
                toolTip1.SetToolTip(input, p.HelpText);
            }

            _paramInputs[p.Key] = input;

            int row = pnlParams.RowCount++;
            pnlParams.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            pnlParams.Controls.Add(label, 0, row);
            pnlParams.Controls.Add(input, 1, row);
        }

        pnlParams.ResumeLayout(true);
        LayoutDestinationPanels();
    }

    // Grows the parameter panel to show every field (no clipping) and stacks the
    // connection buttons and the Publisher settings group below it, so switching to a
    // connector with more parameters (e.g. Kusto) reflows instead of hiding fields.
    private void LayoutDestinationPanels()
    {
        int contentHeight = pnlParams.GetPreferredSize(new Size(pnlParams.Width, 0)).Height;
        pnlParams.Height = Math.Max(contentHeight, 23);

        int buttonsTop = pnlParams.Bottom + 8;
        btnAzureAuth.Top = buttonsTop;
        btnTestCnn.Top = buttonsTop;

        groupBox2.Height = btnTestCnn.Bottom + 12;
        groupBox1.Top = groupBox2.Bottom + 8;
    }

    private void cboTargetType_SelectedIndexChanged(object sender, EventArgs e)
    {
        RebuildParameterInputs();
    }

    /// <summary>
    /// Builds the target connection for the connector and parameters selected in the UI.
    /// </summary>
    private ITargetConnection CreateConnection()
    {
        var values = _paramInputs.ToDictionary(kv => kv.Key, kv => kv.Value.Text);
        return SelectedConnector.CreateConnection(values, _credential);
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsPayloadChanged
    {
        get { return _IsPayloadChanged; }
        set
        {
            _IsPayloadChanged = value;
            RefreshAppTitle();
        }
    }

    private DateTime _runStartUtc = DateTime.MinValue;

    private void ResetCounters()
    {
        _BatchSent = 0;
        _MsgSent = 0;
        _StartTime = DateTime.Now;
        _runStartUtc = DateTime.UtcNow;
        _TotalSizeInBytes = 0;
    }

    /// <summary>
    /// Verifies all required parameters for the selected connector have a value.
    /// Shows a message and focuses the offending field if not.
    /// </summary>
    private bool ValidateRequiredParameters()
    {
        foreach (ConnectionParameter p in SelectedConnector.Parameters)
        {
            if (p.Required && _paramInputs.TryGetValue(p.Key, out var input) && string.IsNullOrWhiteSpace(input.Text))
            {
                lastErrorTextBox.Text = $"Please provide a value for '{p.Label}'.";
                input.Focus();
                return false;
            }
        }
        return true;
    }

    private async void btnRun_Click(object sender, EventArgs e)
    {
        if (!ValidateRequiredParameters())
            return;

        RecalcTotal();
        lastErrorTextBox.Text = "";
        progressBar1.ForeColor = Color.LimeGreen;

        btnRun.Enabled = false;
        progressBar1.Maximum = (int)_TotalBatchCount;
        progressBar1.Value = 0;
        ResetCounters();

        statusBatches.Text = $"Batches sent: {_BatchSent} / {_TotalBatchCount}";
        status.Text = "Status: In Progress...";
        statusTime.Text = "";

        _conn = CreateConnection();

        LoadGenerator generator = new LoadGenerator(new PayloadGenerator(txtPayload.Text, _variables))
        {
            BatchesNo = (int)SettingsBatchesPerSenderNumber.Value,
            EventsPerBatch = (int)SettingsMsgPerBatchNumber.Value,
            WaitTime = new TimeSpan(0, 0, (int)SettingsWaitTimeSec.Value),
        };
        generator.BatchSent += LoadGenerator_BatchSent;
        _CancellationTokenSource = new CancellationTokenSource();

        int concurrency = (int)SettingsParallelismNumber.Value;

        try
        {
            btnCancel.Enabled = true;
            await generator.Send(_conn, concurrency, _CancellationTokenSource.Token);
            btnCancel.Enabled = false;
            if (_CancellationTokenSource.IsCancellationRequested)
            {
                OnCancelled();
            }
            else
            {
                OnCompleted();
                await VerifyIngestionIfRequested();
            }
        }
        catch (Exception ex)
        {
            progressBar1.ForeColor = Color.Red;
            OnFailed(ex.Message);
        }

    }

    // #3: opt-in post-run check for asynchronous (queued) ingestion failures.
    private async Task VerifyIngestionIfRequested()
    {
        if (!chkVerifyIngestion.Checked || _conn is not IIngestionVerifier verifier)
            return;
        try
        {
            IReadOnlyList<string> failures = await verifier.GetIngestionFailuresSinceAsync(_runStartUtc);
            lastErrorTextBox.Text = failures.Count == 0
                ? "Ingestion verification: no failures reported (queued failures may lag)."
                : $"Ingestion verification: {failures.Count} failure(s):\r\n" + string.Join("\r\n", failures);
        }
        catch (Exception ex)
        {
            lastErrorTextBox.Text = $"Ingestion verification could not run: {ex.Message}";
        }
    }

    private void UpdateStatus(string Status)
    {
        _EndTime = DateTime.Now;
        TimeSpan ts = _EndTime - _StartTime;
        status.Text = $"Status: {Status}.";
        statusBatches.Text = $"Sent {_MsgSent} messages in {_TotalBatchCount} batches. Total time: {ts:c} (TPS: {(_MsgSent / ts.TotalSeconds):F2}) | Bytes sent: {_TotalSizeInBytes:0,0} ({(_TotalSizeInBytes / BytesToMbps / ts.TotalSeconds):F2} Mbps)";
    }

    private void OnCompleted()
    {
        progressBar1.Maximum = progressBar1.Value;
        UpdateStatus("Completed");
        btnRun.Enabled = true;
    }

    private void OnCancelled()
    {
        if (progressBar1.Value == 0)
        {
            progressBar1.Value = 1;
        }
        progressBar1.Maximum = progressBar1.Value;
        UpdateStatus("Cancelled");
        btnRun.Enabled = true;
    }

    private void OnFailed(string errorMessage)
    {
        if (progressBar1.Value == 0)
        {
            progressBar1.Value = 1;
        }
        progressBar1.Maximum = progressBar1.Value;
        UpdateStatus("Failed");
        lastErrorTextBox.Text = errorMessage;
        btnRun.Enabled = true;
        btnCancel.Enabled = false;
    }

    // Invoked once per sent batch. With multiple concurrent senders the LoadGenerator
    // raises BatchSent on continuations that resume on this (UI) thread, so these
    // control/counter updates stay single-threaded and safe.
    private void LoadGenerator_BatchSent(object? sender, BatchSentEventArgs e)
    {
        progressBar1.Increment(1);
        _BatchSent++;
        _MsgSent += e.MessageCount;
        TimeSpan ts = DateTime.Now - _StartTime;
        _TotalSizeInBytes += e.SizeInBytes;
        statusBatches.Text = $"Batches sent: {_BatchSent} / {_TotalBatchCount} (TPS: {(_MsgSent / ts.TotalSeconds):F2}) | Bytes sent: {_TotalSizeInBytes:0,0} ({(_TotalSizeInBytes / BytesToMbps / ts.TotalSeconds):F2} Mbps)";
        //https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/tokens/interpolated
    }

    private void RecalcTotal()
    {
        _TotalBatchCount = SettingsParallelismNumber.Value * SettingsBatchesPerSenderNumber.Value;
        _TotalMsgCount = _TotalBatchCount * SettingsMsgPerBatchNumber.Value;
        SettingsTotalMsgCount.Value = _TotalMsgCount;
    }
    private void RefreshAppTitle()
    {
        this.Text = (_IsPayloadChanged ? "*" : "") + (_hasNamedFile ? _FileName + " - " : "") + this.Tag;
    }

    private void SettingsParallelismNumber_ValueChanged(object sender, EventArgs e)
    {
        RecalcTotal();
    }

    private void SettingsBatchesPerSenderNumber_ValueChanged(object sender, EventArgs e)
    {
        RecalcTotal();
    }

    private void SettingsMsgPerBatchNumber_ValueChanged(object sender, EventArgs e)
    {
        RecalcTotal();
    }

    private void exitToolStripMenuItem_Click(object sender, EventArgs e)
    {
        this.Close();
    }

    private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
    {
        SaveFileDialog saveFileDialog = new SaveFileDialog();
        saveFileDialog.Filter = FILES_FILTER;
        saveFileDialog.FileName = _FileName;
        DialogResult dr = saveFileDialog.ShowDialog();
        if (dr == DialogResult.OK && saveFileDialog.FileName.Length > 0)
        {
            SaveFile(saveFileDialog.FileName);
        }
    }

    private void saveMessageTemplateToolStripMenuItem_Click(object sender, EventArgs e)
    {
        SaveFile(_FileName);
    }

    private void SaveFile(string FileName)
    {
        System.IO.File.WriteAllText(FileName, txtPayload.Text);
        _FileName = new System.IO.FileInfo(FileName).Name;
        _hasNamedFile = true;
        IsPayloadChanged = false;
    }

    private void loadMessageTemplateToolStripMenuItem_Click(object sender, EventArgs e)
    {
        OpenFileDialog openFileDialog = new OpenFileDialog();
        openFileDialog.Filter = FILES_FILTER;
        openFileDialog.ShowDialog();
        if (openFileDialog.FileName.Length > 0)
        {
            txtPayload.Text = System.IO.File.ReadAllText(openFileDialog.FileName);
            _FileName = openFileDialog.SafeFileName;
            _hasNamedFile = true;
        }
        IsPayloadChanged = false;
    }

    private void txtPayload_TextChanged(object sender, EventArgs e)
    {
        IsPayloadChanged = true;
    }

    private void Form1_Load(object sender, EventArgs e)
    {
        cboTargetType.Items.Clear();
        foreach (ITargetConnector connector in _connectors)
        {
            cboTargetType.Items.Add(connector.DisplayName);
        }
        cboTargetType.SelectedIndex = 0; // triggers RebuildParameterInputs

        LoadVariableDefinitions();
        TryRestoreCachedSignIn();
        UpdateAuthUi();
        // Loading the default payload during InitializeComponent flags the payload as
        // changed; reset it so the caption starts clean (no leading '*').
        IsPayloadChanged = false; // setter refreshes the title
    }

    // Resolution order: a variables.toml next to the executable, then the built-in
    // embedded defaults. A file chosen via the menu overrides this at runtime.
    private void LoadVariableDefinitions()
    {
        string bundled = System.IO.Path.Combine(AppContext.BaseDirectory, VariableDefinitions.DefaultFileName);
        try
        {
            _variables = System.IO.File.Exists(bundled)
                ? VariableDefinitions.Load(bundled)
                : VariableDefinitions.LoadDefaults();
        }
        catch (Exception ex)
        {
            _variables = VariableDefinitions.LoadDefaults();
            lastErrorTextBox.Text = $"Could not load '{bundled}': {ex.Message}. Using built-in defaults.";
        }
    }

    private void loadVariableDefinitionsToolStripMenuItem_Click(object sender, EventArgs e)
    {
        OpenFileDialog openFileDialog = new OpenFileDialog();
        openFileDialog.Filter = "TOML files (*.toml)|*.toml|All files (*.*)|*.*";
        if (openFileDialog.ShowDialog() == DialogResult.OK && openFileDialog.FileName.Length > 0)
        {
            try
            {
                _variables = VariableDefinitions.Load(openFileDialog.FileName);
                lastErrorTextBox.Text = $"Loaded {_variables.Count} variable definition(s) from {openFileDialog.SafeFileName}.";
            }
            catch (Exception ex)
            {
                lastErrorTextBox.Text = $"Failed to load variable definitions: {ex.Message}";
            }
        }
    }

    private void btnPreview_Click(object sender, EventArgs e)
    {
        PayloadGenerator s = new PayloadGenerator(txtPayload.Text, _variables);
        frmPreview f = new frmPreview();
        f.gen = s;
        f.ShowDialog();
    }

    private async void btnCancel_Click(object sender, EventArgs e)
    {
        progressBar1.ForeColor = Color.Yellow;
        btnCancel.Enabled = false;
        _CancellationTokenSource.Cancel();
    }

    private void frmGenerator_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.KeyData == Keys.F5)
        {
            btnRun_Click(sender, e);
        }
        if (e.KeyData == Keys.F3)
        {
            btnPreview_Click(sender, e);
        }
    }

    private void mnuAbout_Click(object sender, EventArgs e)
    {
        frmAboutApp f = new frmAboutApp();
        f.ShowDialog();
    }

    private async void btnTestCnn_Click(object sender, EventArgs e)
    {
        if (!ValidateRequiredParameters())
            return;

        try
        {
            _conn = CreateConnection();
            await _conn.ConnectAsync();
            lastErrorTextBox.Text = "Connection successful.";
        }
        catch (Exception ex)
        {
            lastErrorTextBox.Text = ex.ToString();
        }
    }

    // The button toggles: sign in when signed out, sign out when signed in.
    private async void btnAzureAuth_Click(object sender, EventArgs e)
    {
        if (_credential != null)
            SignOut();
        else
            await AzureInteractiveAuth();
    }

    private async Task AzureInteractiveAuth()
    {
        try
        {
            var credential = CreatePersistentCredential(null);

            // Interactive sign-in, then persist the authentication record so future
            // runs can silently reuse the token cache (no browser prompt).
            AuthenticationRecord record = await credential.AuthenticateAsync();

            System.IO.Directory.CreateDirectory(AuthCacheDir);
            using (var stream = System.IO.File.Create(AuthRecordPath))
            {
                await record.SerializeAsync(stream);
            }

            _credential = credential;
            _signedInUser = record.Username;
            UpdateAuthUi();
            lastErrorTextBox.Text = $"Signed in as {record.Username}. Sign-in cached for future runs.";
        }
        catch (Exception ex)
        {
            lastErrorTextBox.Text = $"Error: {ex.Message}";
        }
    }

    private void azureSignOutToolStripMenuItem_Click(object sender, EventArgs e)
    {
        SignOut();
    }

    private void SignOut()
    {
        _credential = null;
        _signedInUser = null;
        UpdateAuthUi();
        try
        {
            if (System.IO.File.Exists(AuthRecordPath))
                System.IO.File.Delete(AuthRecordPath);
            lastErrorTextBox.Text = "Signed out. Cached sign-in cleared; connection-string auth will be used until you sign in again.";
        }
        catch (Exception ex)
        {
            lastErrorTextBox.Text = $"Signed out, but could not delete the cached record: {ex.Message}";
        }
    }


}
