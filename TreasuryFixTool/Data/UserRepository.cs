using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using TreasuryFixTool.Models;

namespace TreasuryFixTool.Data;

public class UserRepository
{
    private readonly string _connectionString;

    public UserRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task InitializeAsync()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        var tableExists = await connection.ExecuteScalarAsync<bool>(@"
            SELECT EXISTS (
                SELECT FROM information_schema.tables 
                WHERE table_name = 'users'
            )");

        if (!tableExists)
        {
            await connection.ExecuteAsync(@"
                CREATE TABLE users (
                    id SERIAL PRIMARY KEY,
                    username VARCHAR(100) UNIQUE NOT NULL,
                    email VARCHAR(255) UNIQUE NOT NULL,
                    password_hash VARCHAR(255) NOT NULL,
                    full_name VARCHAR(200),
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    last_login TIMESTAMP,
                    is_active BOOLEAN DEFAULT true
                )");
        }
        else
        {
            var hasUserId = await connection.ExecuteScalarAsync<bool>(@"
                SELECT EXISTS (
                    SELECT FROM information_schema.columns 
                    WHERE table_name = 'users' AND column_name = 'user_id'
                )");

            if (hasUserId)
            {
                await connection.ExecuteAsync("ALTER TABLE users RENAME COLUMN user_id TO id");
            }

            var hasPasswordHash = await connection.ExecuteScalarAsync<bool>(@"
                SELECT EXISTS (
                    SELECT FROM information_schema.columns 
                    WHERE table_name = 'users' AND column_name = 'passwordhash'
                )");

            if (hasPasswordHash)
            {
                await connection.ExecuteAsync("ALTER TABLE users RENAME COLUMN passwordhash TO password_hash");
            }

            var hasCreatedDate = await connection.ExecuteScalarAsync<bool>(@"
                SELECT EXISTS (
                    SELECT FROM information_schema.columns 
                    WHERE table_name = 'users' AND column_name = 'createddate'
                )");

            if (hasCreatedDate)
            {
                await connection.ExecuteAsync("ALTER TABLE users RENAME COLUMN createddate TO created_at");
            }

            var hasLastLogin = await connection.ExecuteScalarAsync<bool>(@"
                SELECT EXISTS (
                    SELECT FROM information_schema.columns 
                    WHERE table_name = 'users' AND column_name = 'lastlogin'
                )");

            if (hasLastLogin)
            {
                await connection.ExecuteAsync("ALTER TABLE users RENAME COLUMN lastlogin TO last_login");
            }

            var hasFullName = await connection.ExecuteScalarAsync<bool>(@"
                SELECT EXISTS (
                    SELECT FROM information_schema.columns 
                    WHERE table_name = 'users' AND column_name = 'fullname'
                )");

            if (hasFullName)
            {
                await connection.ExecuteAsync("ALTER TABLE users RENAME COLUMN fullname TO full_name");
            }
        }
    }

    public async Task<User?> GetUserByIdAsync(int id)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        const string sql = @"
            SELECT 
                id AS Id, 
                username AS Username, 
                email AS Email, 
                password_hash AS PasswordHash, 
                full_name AS FullName, 
                created_at AS CreatedAt, 
                last_login AS LastLogin, 
                is_active AS IsActive 
            FROM users 
            WHERE id = @Id";

        return await connection.QueryFirstOrDefaultAsync<User>(sql, new { Id = id });
    }

    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        const string sql = @"
            SELECT 
                id AS Id, 
                username AS Username, 
                email AS Email, 
                password_hash AS PasswordHash, 
                full_name AS FullName, 
                created_at AS CreatedAt, 
                last_login AS LastLogin, 
                is_active AS IsActive 
            FROM users 
            WHERE username = @Username";

        return await connection.QueryFirstOrDefaultAsync<User>(sql, new { Username = username });
    }

    public async Task<int> CreateUserAsync(User user)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        const string sql = @"
            INSERT INTO users (username, email, password_hash, full_name, created_at, is_active)
            VALUES (@Username, @Email, @PasswordHash, @FullName, @CreatedAt, @IsActive)
            RETURNING id";

        return await connection.ExecuteScalarAsync<int>(sql, user);
    }

    public async Task<bool> UpdateUserAsync(User user)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        const string sql = @"
            UPDATE users 
            SET username = @Username, 
                email = @Email, 
                password_hash = @PasswordHash, 
                full_name = @FullName, 
                last_login = @LastLogin, 
                is_active = @IsActive
            WHERE id = @Id";

        var rowsAffected = await connection.ExecuteAsync(sql, user);
        return rowsAffected > 0;
    }

    public async Task<bool> DeleteUserAsync(int id)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        const string sql = "DELETE FROM users WHERE id = @Id";

        var rowsAffected = await connection.ExecuteAsync(sql, new { Id = id });
        return rowsAffected > 0;
    }

    public async Task<IEnumerable<User>> GetAllUsersAsync()
    {
        using var connection = new NpgsqlConnection(_connectionString);
        const string sql = @"
            SELECT 
                id AS Id, 
                username AS Username, 
                email AS Email, 
                password_hash AS PasswordHash, 
                full_name AS FullName, 
                created_at AS CreatedAt, 
                last_login AS LastLogin, 
                is_active AS IsActive 
            FROM users 
            ORDER BY id";

        return await connection.QueryAsync<User>(sql);
    }

    public async Task<User?> ValidateLoginAsync(string username, string password)
    {
        await InitializeAsync();

        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        const string sql = @"
            SELECT id AS Id, username AS Username, email AS Email, password_hash AS PasswordHash, 
                   full_name AS FullName, created_at AS CreatedAt, last_login AS LastLogin, is_active AS IsActive
            FROM users
            WHERE username = @Username OR email = @Username
            LIMIT 1;";

        var user = await conn.QuerySingleOrDefaultAsync<User>(sql, new { Username = username });

        if (user == null)
            return null;

        if (user.PasswordHash != HashPassword(password))
            return null;

        await conn.ExecuteAsync(
            "UPDATE users SET last_login = NOW() WHERE id = @Id",
            new { Id = user.Id });

        user.PasswordHash = string.Empty;
        return user;
    }

    public async Task<int> CreateUserAsync(string fullName, string email, string username, string passwordHash, string? department = null, string role = "officer")
    {
        await InitializeAsync();

        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        const string sql = @"
            INSERT INTO users (username, email, password_hash, full_name, created_at, is_active)
            VALUES (@Username, @Email, @PasswordHash, @FullName, @CreatedAt, true)
            RETURNING id;";

        var id = await conn.ExecuteScalarAsync<int>(sql, new
        {
            Username = username,
            Email = email.ToLower(),
            PasswordHash = passwordHash,
            FullName = fullName,
            CreatedAt = DateTime.UtcNow
        });

        return id;
    }

    private const string StaticSalt = "TreasuryFixTool2026Salt!";

    internal static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var combined = StaticSalt + password;
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
        return Convert.ToBase64String(hash);
    }
}