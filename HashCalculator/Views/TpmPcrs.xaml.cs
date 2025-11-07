using System;
using Windows.Storage;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace TPMPCRCalculator.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class TpmPcrs : Page
    {
        private ApplicationDataContainer m_RoamingSettings = null;
        private const string m_InitialPcrValue = "InitialPcrValue";
        private const string m_CustomPcrValue = "CustomPcrValue";
        private const string m_StartupType = "StartupType";

        private int m_CurrentAlgorithmIndex = 0;
        private readonly NavigationHelper m_NavigationHelper;
        private const string m_SettingSelectedHashAlgorithm = "pcrSelectedHash";
        private const string m_SettingInput = "pcrInput";
        private const string m_SettingPCR = "pcrPCR";
        private const string m_SettingLocality = "pcrLocality";
        private const string m_SettingCustomValue = "pcrCustomValue";
        private const string m_SettingStartupType = "pcrStartupType";
        private const string m_ExtendDescriptionTemplate =
            "The Extend button concatenates the current PCR value with the provided input data (hash) " +
            "and then computes a hash of the concatenated value. " +
            "This computed hash is then displayed as Current PCR Value.";

        public TpmPcrs()
        {
            this.InitializeComponent();

            this.m_RoamingSettings = ApplicationData.Current.RoamingSettings;

            this.m_NavigationHelper = new NavigationHelper(this);
            this.m_NavigationHelper.LoadState += LoadState;
            this.m_NavigationHelper.SaveState += SaveState;

            string[] algorithms = Worker.GetHashingAlgorithms();
            ListOfAlgorithms.Items.Clear();
            for (uint i = 0; i < algorithms.Length; i++)
            {
                ListOfAlgorithms.Items.Add(algorithms[i]);
            }
            ListOfAlgorithms.SelectedIndex = m_CurrentAlgorithmIndex;

            // Set initial state for radio button controls after initialization
            SetInitialControlState();

            ResetPcrText();
            ExtendDescription.Text = m_ExtendDescriptionTemplate;
        }

        private void SetInitialControlState()
        {
            // Ensure controls are available before setting their state
            if (LocalityRadioButton != null && CustomValueRadioButton != null &&
                LocalityInput != null && SetLocalityButton != null &&
                CustomValueInput != null && SetCustomValueButton != null)
            {
                // Default to Locality mode
                LocalityRadioButton.IsChecked = true;
                LocalityInput.IsEnabled = true;
                SetLocalityButton.IsEnabled = true;
                CustomValueInput.IsEnabled = false;
                SetCustomValueButton.IsEnabled = false;
            }
        }

        private void StartupTypeRadioButton_Checked(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            // Ensure all controls are initialized before accessing them
            if (LocalityRadioButton == null || CustomValueRadioButton == null || 
                LocalityInput == null || SetLocalityButton == null ||
                CustomValueInput == null || SetCustomValueButton == null)
            {
                return;
            }

            if (LocalityRadioButton.IsChecked == true)
            {
                // Enable locality controls, disable custom value controls
                LocalityInput.IsEnabled = true;
                SetLocalityButton.IsEnabled = true;
                CustomValueInput.IsEnabled = false;
                SetCustomValueButton.IsEnabled = false;
            }
            else if (CustomValueRadioButton.IsChecked == true)
            {
                // Enable custom value controls, disable locality controls
                LocalityInput.IsEnabled = false;
                SetLocalityButton.IsEnabled = false;
                CustomValueInput.IsEnabled = true;
                SetCustomValueButton.IsEnabled = true;
            }
        }

        private void ResetPcrText()
        {
            if (ListOfAlgorithms.SelectedIndex != -1)
            {
                string algorithmName = (string)ListOfAlgorithms.SelectedItem;
                PCR.Text = Worker.GetZeroDigestForAlgorithm(algorithmName);
                
                // Check for stored startup values
                if (m_RoamingSettings.Values[m_StartupType] != null)
                {
                    string startupType = (string)m_RoamingSettings.Values[m_StartupType];
                    if (startupType == "Locality" && m_RoamingSettings.Values[m_InitialPcrValue] != null)
                    {
                        PCR.Text = Worker.GetZeroDigestForAlgorithm(algorithmName, (int)m_RoamingSettings.Values[m_InitialPcrValue]);
                    }
                    else if (startupType == "Custom" && m_RoamingSettings.Values[m_CustomPcrValue] != null)
                    {
                        try
                        {
                            PCR.Text = Worker.GetCustomDigestForAlgorithm(algorithmName, (string)m_RoamingSettings.Values[m_CustomPcrValue]);
                        }
                        catch
                        {
                            // If custom value is invalid, fall back to zero digest
                            PCR.Text = Worker.GetZeroDigestForAlgorithm(algorithmName);
                        }
                    }
                }
            }
        }

        private void ResetPcr_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            ResetPcrText();
            ExtendDescription.Text = m_ExtendDescriptionTemplate;
        }

        private void SetStartupValue_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                if (ListOfAlgorithms.SelectedIndex == -1)
                {
                    ExtendDescription.Text = "Please select a hash algorithm first.";
                    return;
                }

                string algorithmName = (string)ListOfAlgorithms.SelectedItem;

                if (LocalityRadioButton.IsChecked == true)
                {
                    // Handle locality setting
                    int localityValue = 0;
                    if (!string.IsNullOrWhiteSpace(LocalityInput.Text))
                    {
                        if (!int.TryParse(LocalityInput.Text, out localityValue))
                        {
                            ExtendDescription.Text = "Invalid locality value. Please enter a valid integer.";
                            return;
                        }
                        if (localityValue < 0 || localityValue > 32)
                        {
                            ExtendDescription.Text = "Locality value must be between 0 and 32.";
                            return;
                        }
                    }

                    PCR.Text = Worker.GetZeroDigestForAlgorithm(algorithmName, localityValue);
                    
                    // Store the locality value and type for future resets
                    m_RoamingSettings.Values[m_InitialPcrValue] = localityValue;
                    m_RoamingSettings.Values[m_StartupType] = "Locality";
                    m_RoamingSettings.Values.Remove(m_CustomPcrValue);
                    
                    ExtendDescription.Text = $"Startup locality set to {localityValue}. Current PCR value updated.";
                }
                else if (CustomValueRadioButton.IsChecked == true)
                {
                    // Handle custom value setting
                    string customValue = CustomValueInput.Text?.Trim() ?? "";
                    
                    PCR.Text = Worker.GetCustomDigestForAlgorithm(algorithmName, customValue);
                    
                    // Store the custom value and type for future resets
                    m_RoamingSettings.Values[m_CustomPcrValue] = customValue;
                    m_RoamingSettings.Values[m_StartupType] = "Custom";
                    m_RoamingSettings.Values.Remove(m_InitialPcrValue);
                    
                    if (string.IsNullOrWhiteSpace(customValue))
                    {
                        ExtendDescription.Text = "PCR reset to zero value.";
                    }
                    else
                    {
                        ExtendDescription.Text = $"Custom startup value set. Current PCR value updated to: {PCR.Text}";
                    }
                }
            }
            catch (Exception ex)
            {
                ExtendDescription.Text = $"Error setting startup value: {ex.Message}";
            }
        }

        private void Extend_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            string algorithmName = (string)ListOfAlgorithms.SelectedItem;
            // if input cannot be hashed, don't change PCR values
            try
            {
                string inputHash = Worker.ComputeHash(algorithmName, Input.Text, true);
                if (inputHash != null &&
                    inputHash.Length > 0)
                {
                    string oldPCR = PCR.Text;
                    PCR.Text = Worker.ComputeHash(algorithmName, PCR.Text + Input.Text, true);
                    ExtendDescription.Text = m_ExtendDescriptionTemplate + "\n\n" +
                        "Old PCR Value: " + oldPCR + "\n" +
                        "Input Hash: " + Input.Text + "\n" +
                        "Concatenated Value: " + oldPCR + Input.Text + "\n" +
                        "New PCR Value: " + PCR.Text;
                }
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(ex.Message))
                {
                    ExtendDescription.Text = ex.Message;
                }
            }
        }

        private void TpmPcr_KeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            switch (e.Key)
            {
                case Windows.System.VirtualKey.Enter:
                    Extend_Click(sender, e);
                    break;
            }
        }

        private void ListOfAlgorithms_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListOfAlgorithms.SelectedIndex != m_CurrentAlgorithmIndex)
            {
                m_CurrentAlgorithmIndex = ListOfAlgorithms.SelectedIndex;
                ResetPcrText();
                ExtendDescription.Text = m_ExtendDescriptionTemplate;
            }
        }

        #region Save and Restore state

        /// <summary>
        /// Populates the page with content passed during navigation. Any saved state is also
        /// provided when recreating a page from a prior session.
        /// </summary>
        /// <param name="sender">
        /// The source of the event; typically <see cref="NavigationHelper"/>.
        /// </param>
        /// <param name="e">Event data that provides both the navigation parameter passed to
        /// <see cref="Frame.Navigate(Type, Object)"/> when this page was initially requested and
        /// a dictionary of state preserved by this page during an earlier
        /// session. The state will be null the first time a page is visited.</param>
        private void LoadState(object sender, LoadStateEventArgs e)
        {
            if (SuspensionManager.SessionState.ContainsKey(m_SettingSelectedHashAlgorithm))
            {
                int index;
                if (Int32.TryParse((string)SuspensionManager.SessionState[m_SettingSelectedHashAlgorithm], out index))
                {
                    if (index >= 0 && index < ListOfAlgorithms.Items.Count)
                    {
                        ListOfAlgorithms.SelectedIndex = index;
                        m_CurrentAlgorithmIndex = index;
                    }
                }
            }

            if (SuspensionManager.SessionState.ContainsKey(m_SettingInput))
            {
                Input.Text = (string)SuspensionManager.SessionState[m_SettingInput];
            }

            if (SuspensionManager.SessionState.ContainsKey(m_SettingPCR))
            {
                PCR.Text = (string)SuspensionManager.SessionState[m_SettingPCR];
            }

            if (SuspensionManager.SessionState.ContainsKey(m_SettingLocality))
            {
                LocalityInput.Text = (string)SuspensionManager.SessionState[m_SettingLocality];
            }

            if (SuspensionManager.SessionState.ContainsKey(m_SettingCustomValue))
            {
                CustomValueInput.Text = (string)SuspensionManager.SessionState[m_SettingCustomValue];
            }

            if (SuspensionManager.SessionState.ContainsKey(m_SettingStartupType))
            {
                string startupType = (string)SuspensionManager.SessionState[m_SettingStartupType];
                
                // Ensure controls are available before setting their state
                if (LocalityRadioButton != null && CustomValueRadioButton != null)
                {
                    if (startupType == "Custom")
                    {
                        CustomValueRadioButton.IsChecked = true;
                        StartupTypeRadioButton_Checked(CustomValueRadioButton, null);
                    }
                    else
                    {
                        LocalityRadioButton.IsChecked = true;
                        StartupTypeRadioButton_Checked(LocalityRadioButton, null);
                    }
                }
            }
        }

        /// <summary>
        /// Preserves state associated with this page in case the application is suspended or the
        /// page is discarded from the navigation cache. Values must conform to the serialization
        /// requirements of <see cref="SuspensionManager.SessionState"/>.
        /// </summary>
        /// <param name="sender">The source of the event; typically <see cref="NavigationHelper"/>.</param>
        /// <param name="e">Event data that provides an empty dictionary to be populated with
        /// serializable state.</param>
        private void SaveState(object sender, SaveStateEventArgs e)
        {
            SuspensionManager.SessionState[m_SettingSelectedHashAlgorithm] = ListOfAlgorithms.SelectedIndex.ToString();
            SuspensionManager.SessionState[m_SettingInput] = Input.Text;
            SuspensionManager.SessionState[m_SettingPCR] = PCR.Text;
            SuspensionManager.SessionState[m_SettingLocality] = LocalityInput.Text;
            SuspensionManager.SessionState[m_SettingCustomValue] = CustomValueInput.Text;
            SuspensionManager.SessionState[m_SettingStartupType] = LocalityRadioButton.IsChecked == true ? "Locality" : "Custom";
        }

        #endregion

        #region NavigationHelper registration

        /// <summary>
        /// The methods provided in this section are simply used to allow
        /// NavigationHelper to respond to the page's navigation methods.
        /// <para>
        /// Page specific logic should be placed in event handlers for the  
        /// <see cref="NavigationHelper.LoadState"/>
        /// and <see cref="NavigationHelper.SaveState"/>.
        /// The navigation parameter is available in the LoadState method 
        /// in addition to page state preserved during an earlier session.
        /// </para>
        /// </summary>
        /// <param name="e">Provides data for navigation methods and event
        /// handlers that cannot cancel the navigation request.</param>
        /// 
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            this.m_NavigationHelper.OnNavigatedTo(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            this.m_NavigationHelper.OnNavigatedFrom(e);
        }

        #endregion
    }
}
