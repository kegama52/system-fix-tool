using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.Configuration;
using Npgsql;
using TreasuryFixTool.Data;
using TreasuryFixTool.Models;

namespace TreasuryFixTool.Views
{
    public class LoginEventArgs : EventArgs
    {
        public int           UserId      { get; set; }
        public string        Username    { get; set; } = string.Empty;
        public string        FullName    { get; set; } = string.Empty;
        public string        Email       { get; set; } = string.Empty;
        public string        Department  { get; set; } = string.Empty;
        public string        Role        { get; set; } = string.Empty;
        public bool          IsSuccess   { get; set; }
        public string        ErrorMessage { get; set; } = string.Empty;
    }

    public partial class LoginUserControl : UserControl
    {
        private readonly string            _connectionString;
        private readonly UserRepository    _userRepo;
        private readonly IConfiguration    _config;

        public event EventHandler<LoginEventArgs>? LoginSuccessful;
        private bool _isPasswordVisible = false;

        public LoginUserControl()
        {
            InitializeComponent();

            _config = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            _connectionString = _config["Database:ConnectionString"]
                ?? throw new InvalidOperationException("Database connection string missing in appsettings.json");

            _userRepo = new UserRepository(_connectionString);
        }

        // --- PANEL SWITCHING NAVIGATION ---
        private void LinkRegister_Click(object sender, MouseButtonEventArgs e)
        {
            PanelLogin.Visibility = Visibility.Collapsed;
            PanelRegister.Visibility = Visibility.Visible;
        }

        private void LinkSignIn_Click(object sender, MouseButtonEventArgs e)
        {
            PanelRegister.Visibility = Visibility.Collapsed;
            PanelLogin.Visibility = Visibility.Visible;
        }

        private void LinkStaffRegister_Click(object sender, MouseButtonEventArgs e)
        {
            var staffReg = new RegistrationWindow();
            staffReg.ShowDialog();
        }

        // --- REGISTRATION ---
        private async void BtnSubmitRegister_Click(object sender, RoutedEventArgs e)
        {
            string fullName  = TxtRegFullName.Text.Trim();
            string email     = TxtRegEmail.Text.Trim();
            string username  = TxtRegUsername.Text.Trim();
            string password  = TxtRegPassword.Password;
            string confirm   = TxtRegConfirmPassword.Password;
            string department = ComboDepartment.SelectedItem is ComboBoxItem item
                                   ? item.Content?.ToString() ?? ""
                                   : "";

            if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(email) ||
                string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Please fill out all registration fields.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (password != confirm)
            {
                MessageBox.Show("Passwords do not match. Please verify.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (password.Length < 8)
            {
                MessageBox.Show("Password must be at least 8 characters.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string passwordHash = HashPassword(password);

                await _userRepo.CreateUserAsync(
                    fullName, email, username, passwordHash);

                MessageBox.Show($"Account for \"{fullName}\" created successfully!\n\nYou can now sign in with your username and password.",
                    "Registration Successful", MessageBoxButton.OK, MessageBoxImage.Information);

                TxtRegFullName.Clear();
                TxtRegEmail.Clear();
                TxtRegUsername.Clear();
                TxtRegPassword.Clear();
                TxtRegConfirmPassword.Clear();

                PanelRegister.Visibility = Visibility.Collapsed;
                PanelLogin.Visibility = Visibility.Visible;
            }
            catch (PostgresException ex) when (ex.SqlState == "23505")
            {
                MessageBox.Show("A user with this username or email already exists.\nPlease choose a different username or email address.",
                    "Registration Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Registration failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // --- SIGN IN ---
        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            string username = TxtUsername.Text.Trim();
            string password = _isPasswordVisible ? TxtPasswordUnmasked.Text : TxtPassword.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Please enter your username and password.", "Sign In",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                User? user = await _userRepo.ValidateLoginAsync(username, password);

                if (user is not null)
                {
                    var args = new LoginEventArgs
                    {
                        UserId    = user.Id,
                        Username  = user.Username,
                        FullName  = user.FullName,
                        Email     = user.Email,
                        IsSuccess = true
                    };
                    LoginSuccessful?.Invoke(this, args);
                }
                else
                {
                    MessageBox.Show("The username or password you entered is incorrect.\nPlease try again.",
                        "Sign In Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Sign-in error: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnTogglePassword_Click(object sender, RoutedEventArgs e)
        {
            if (!_isPasswordVisible)
            {
                TxtPasswordUnmasked.Text = TxtPassword.Password;
                TxtPassword.Visibility           = Visibility.Collapsed;
                TxtPasswordUnmasked.Visibility   = Visibility.Visible;
                BtnTogglePassword.Content        = "🙈";
                _isPasswordVisible               = true;
            }
            else
            {
                TxtPassword.Password = TxtPasswordUnmasked.Text;
                TxtPasswordUnmasked.Visibility = Visibility.Collapsed;
                TxtPassword.Visibility         = Visibility.Visible;
                BtnTogglePassword.Content      = "👁";
                _isPasswordVisible             = false;
            }
        }

        private static string HashPassword(string password) => UserRepository.HashPassword(password);
    }
}
