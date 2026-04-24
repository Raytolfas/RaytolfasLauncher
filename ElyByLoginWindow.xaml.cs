using System.Windows;
using WpfMessageBox = System.Windows.MessageBox;

namespace RaytolfasLauncher
{
    public partial class ElyByLoginWindow : Window
    {
        private readonly string language;

        public string LoginValue => LoginBox.Text.Trim();
        public string PasswordValue => PasswordBox.Password;
        public string TotpValue => TotpBox.Text.Trim();

        public ElyByLoginWindow(string language)
        {
            InitializeComponent();
            this.language = LocalizationManager.NormalizeLanguage(language);
            ApplyLocalization();
        }

        private string T(string key) => LocalizationManager.Get(key, language);

        private void ApplyLocalization()
        {
            Title = T("elyby.window_title");
            TitleText.Text = T("elyby.title");
            LoginLabel.Text = T("elyby.login");
            PasswordLabel.Text = T("elyby.password");
            TotpLabel.Text = T("elyby.totp");
            HintText.Text = T("elyby.hint");
            CancelButton.Content = T("account.cancel");
            LoginButton.Content = T("elyby.sign_in");
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(LoginValue) || string.IsNullOrWhiteSpace(PasswordValue))
            {
                WpfMessageBox.Show(T("elyby.enter_credentials"));
                return;
            }

            DialogResult = true;
            Close();
        }
    }
}
