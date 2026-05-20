using System;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using TreasuryFixTool.Models;

namespace TreasuryFixTool.Data;

public class TicketRepository
{
    private readonly string _connectionString;

    public TicketRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task InitializeAsync()
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        // Ensure the support_tickets table exists
        var tableExists = await conn.ExecuteScalarAsync<int>(
            @"SELECT COUNT(*) FROM information_schema.tables 
              WHERE table_schema = 'public' AND table_name = 'support_tickets'") > 0;

        if (!tableExists)
        {
            const string createTableSql = @"
                CREATE TABLE support_tickets (
                    ticket_id         VARCHAR(50)   PRIMARY KEY,
                    department        VARCHAR(50),
                    priority          VARCHAR(20),
                    category          VARCHAR(50),
                    description       TEXT          NOT NULL,
                    steps_taken       TEXT,
                    contact_name      VARCHAR(100),
                    contact_phone     VARCHAR(20),
                    machine_name      VARCHAR(100),
                    os_version        VARCHAR(100),
                    status            VARCHAR(20)   DEFAULT 'Open',
                    detected_issues   TEXT,
                    created_at        TIMESTAMP     DEFAULT NOW(),
                    updated_at        TIMESTAMP     DEFAULT NOW()
                );

                CREATE INDEX idx_support_tickets_status  ON support_tickets(status);
                CREATE INDEX idx_support_tickets_created ON support_tickets(created_at DESC);";

            await conn.ExecuteAsync(createTableSql);
        }
    }

    public async Task InsertTicketAsync(Ticket ticket)
    {
        await InitializeAsync();

        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        const string sql = @"
            INSERT INTO support_tickets (ticket_id, department, priority, category, description, 
                steps_taken, contact_name, contact_phone, machine_name, os_version, status, 
                detected_issues, created_at)
            VALUES (@TicketId, @Department, @Priority, @Category, @Description,
                @StepsTaken, @ContactName, @ContactPhone, @MachineName, @OSVersion,
                @Status, @DetectedIssues, @CreatedAt);
            
            UPDATE support_tickets SET updated_at = NOW() WHERE ticket_id = @TicketId;";

        await conn.ExecuteAsync(sql, ticket);
    }
}