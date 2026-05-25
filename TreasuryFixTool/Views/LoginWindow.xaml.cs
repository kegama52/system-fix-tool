using System;
using System.Windows;
using Microsoft.Extensions.Configuration;
using TreasuryFixTool.Data;
using TreasuryFixTool.Models;
namespace TreasuryFixTool.Views;

public partial class LoginWindow : Window
{
    private readonly UserRepository _userRepo;

    public LoginWindow()
    {
        InitializeComponent();

        var config = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        string connStr = config["Database:ConnectionString"]
            ?? throw new InvalidOperationException("Database connection string missing in appsettings.json");

        _userRepo = new UserRepository(connStr);
    }

    private async void LoginUserControl_LoginSuccessful(object sender, LoginEventArgs args)
    {
        if (!args.IsSuccess)
            return;

        DialogResult = true;
        Close();
    }
}
