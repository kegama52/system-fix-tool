using System;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using Npgsql;

namespace TreasuryFixTool.Views
{
    public partial class RegistrationWindow : Window
    {
        private readonly string _connString;

        public RegistrationWindow()
        {
            InitializeComponent();
            _connString = "Host=localhost;Port=5324;Database=tiisgs_db;Username=postgres;Password=NewSecurePass123!;";
            LoadUnits();
        }

        private async void LoadUnits()
        {
            try
            {
                using var conn = new NpgsqlConnection(_connString);
                await conn.OpenAsync();
                using var cmd = new NpgsqlCommand("SELECT id, code, name FROM units WHERE is_active = true ORDER BY name", conn);
                using var reader = await cmd.ExecuteReaderAsync();
                
                while (await reader.ReadAsync())
                {
                    var id = reader.GetGuid(0);
                    var code = reader.GetString(1);
                    var name = reader.GetString(2);
                    CboUnit.Items.Add(new { Id = id, Display = $"{code} - {name}" });
                }
                
                if (CboUnit.Items.Count > 0)
                    CboUnit.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load units: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Guid? GetSelectedUnitId()
        {
            if (CboUnit.SelectedItem != null)
            {
                var prop = CboUnit.SelectedItem.GetType().GetProperty("Id");
                return prop?.GetValue(CboUnit.SelectedItem) as Guid?;
            }
            return null;
        }

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var salt = "TreasuryFixTool2026Salt!"; // Static salt - consider using per-user salt in production
            var combined = salt + password;
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
            return Convert.ToBase64String(hash);
        }

        private async void BtnRegister_Click(object sender, RoutedEventArgs e)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(TxtGovernmentId.Text) || 
                string.IsNullOrWhiteSpace(TxtFirstName.Text) ||
                string.IsNullOrWhiteSpace(TxtLastName.Text) ||
                string.IsNullOrWhiteSpace(TxtEmail.Text) ||
                string.IsNullOrWhiteSpace(TxtPassword.Password))
            {
                MessageBox.Show("All required fields (*) must be completed.", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (TxtPassword.Password != TxtConfirmPassword.Password)
            {
                MessageBox.Show("Passwords do not match.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (TxtPassword.Password.Length < 8)
            {
                MessageBox.Show("Password must be at least 8 characters.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var passwordHash = HashPassword(TxtPassword.Password);
            var role = ((System.Windows.Controls.ComboBoxItem)CboRole.SelectedItem)?.Content?.ToString()?.ToLower().Replace(" ", "_") ?? "officer";

            try
            {
                using var conn = new NpgsqlConnection(_connString);
                await conn.OpenAsync();
                
                var query = @"INSERT INTO users (id, government_id, email, password_hash, first_name, last_name, phone_number, role, unit_id) 
                             VALUES (uuid_generate_v4(), @GovernmentId, @Email, @PasswordHash, @FirstName, @LastName, @Phone, @Role, @UnitId)";
                
                using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@GovernmentId", TxtGovernmentId.Text.Trim());
                cmd.Parameters.AddWithValue("@Email", TxtEmail.Text.Trim().ToLower());
                cmd.Parameters.AddWithValue("@PasswordHash", passwordHash);
                cmd.Parameters.AddWithValue("@FirstName", TxtFirstName.Text.Trim());
                cmd.Parameters.AddWithValue("@LastName", TxtLastName.Text.Trim());
                cmd.Parameters.AddWithValue("@Phone", TxtPhone.Text.Trim());
                cmd.Parameters.AddWithValue("@Role", role);
                
                var unitId = GetSelectedUnitId();
                cmd.Parameters.AddWithValue("@UnitId", unitId.HasValue ? unitId.Value : (object)DBNull.Value);
                
                await cmd.ExecuteNonQueryAsync();
                
                MessageBox.Show("Staff member registered successfully!", "Success", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (NpgsqlException ex) when (ex.SqlState == "23505")
            {
                MessageBox.Show("A user with this Government ID or Email already exists.", "Duplicate Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Registration failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}