using Microsoft.Data.Sqlite;
using SparkForge.Models;

namespace SparkForge.Services;

public class ChatStorageService : IDisposable
{
    private readonly SqliteConnection _db;
    private const int CurrentSchemaVersion = 1;

    public ChatStorageService(string dbPath)
    {
        _db = new SqliteConnection($"Data Source={dbPath}");
        _db.Open();
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        // Create schema version table first
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS schema_version (
                version INTEGER NOT NULL
            );
        ";
        cmd.ExecuteNonQuery();

        // Check current version
        var version = 0;
        cmd.CommandText = "SELECT COALESCE((SELECT version FROM schema_version ORDER BY version DESC LIMIT 1), 0)";
        var result = cmd.ExecuteScalar();
        if (result != null) version = Convert.ToInt32(result);

        Logger.Info($"Database schema version: {version}, current: {CurrentSchemaVersion}");

        if (version < CurrentSchemaVersion)
        {
            Migrate(version);
        }
    }

    private void Migrate(int fromVersion)
    {
        Logger.Info($"Running migrations from v{fromVersion} to v{CurrentSchemaVersion}");

        using var cmd = _db.CreateCommand();

        if (fromVersion < 1)
        {
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS conversations (
                    id TEXT PRIMARY KEY,
                    title TEXT NOT NULL DEFAULT 'New Chat',
                    model TEXT NOT NULL DEFAULT '',
                    system_prompt TEXT NOT NULL DEFAULT '',
                    created_at TEXT NOT NULL,
                    updated_at TEXT NOT NULL
                );

                CREATE TABLE IF NOT EXISTS messages (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    conversation_id TEXT NOT NULL,
                    role TEXT NOT NULL,
                    content TEXT NOT NULL,
                    timestamp TEXT NOT NULL,
                    FOREIGN KEY (conversation_id) REFERENCES conversations(id) ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS idx_messages_conv
                    ON messages(conversation_id, timestamp);
            ";
            cmd.ExecuteNonQuery();
            Logger.Info("Migration v1 complete: created conversations and messages tables");
        }

        // Future migrations go here:
        // if (fromVersion < 2) { ... }

        // Update schema version
        cmd.CommandText = "INSERT INTO schema_version (version) VALUES (@v)";
        cmd.Parameters.AddWithValue("@v", CurrentSchemaVersion);
        cmd.ExecuteNonQuery();

        Logger.Info($"Database migrated to v{CurrentSchemaVersion}");
    }

    public List<Conversation> GetConversations()
    {
        var conversations = new List<Conversation>();
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT id, title, model, system_prompt, created_at, updated_at FROM conversations ORDER BY updated_at DESC";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            conversations.Add(new Conversation
            {
                Id = reader.GetString(0),
                Title = reader.GetString(1),
                Model = reader.GetString(2),
                SystemPrompt = reader.GetString(3),
                CreatedAt = DateTime.Parse(reader.GetString(4)),
                UpdatedAt = DateTime.Parse(reader.GetString(5))
            });
        }
        return conversations;
    }

    public Conversation CreateConversation(string model, string systemPrompt = "")
    {
        var conv = new Conversation
        {
            Model = model,
            SystemPrompt = systemPrompt
        };

        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO conversations (id, title, model, system_prompt, created_at, updated_at)
            VALUES (@id, @title, @model, @sp, @ca, @ua)";
        cmd.Parameters.AddWithValue("@id", conv.Id);
        cmd.Parameters.AddWithValue("@title", conv.Title);
        cmd.Parameters.AddWithValue("@model", conv.Model);
        cmd.Parameters.AddWithValue("@sp", conv.SystemPrompt);
        cmd.Parameters.AddWithValue("@ca", conv.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@ua", conv.UpdatedAt.ToString("O"));
        cmd.ExecuteNonQuery();

        Logger.Debug($"Created conversation: {conv.Id}");
        return conv;
    }

    public void UpdateConversationTitle(string conversationId, string title)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "UPDATE conversations SET title = @title, updated_at = @ua WHERE id = @id";
        cmd.Parameters.AddWithValue("@title", title);
        cmd.Parameters.AddWithValue("@ua", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@id", conversationId);
        cmd.ExecuteNonQuery();
    }

    public void UpdateConversationModel(string conversationId, string model)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "UPDATE conversations SET model = @model, updated_at = @ua WHERE id = @id";
        cmd.Parameters.AddWithValue("@model", model);
        cmd.Parameters.AddWithValue("@ua", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@id", conversationId);
        cmd.ExecuteNonQuery();
    }

    public void UpdateSystemPrompt(string conversationId, string systemPrompt)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "UPDATE conversations SET system_prompt = @sp, updated_at = @ua WHERE id = @id";
        cmd.Parameters.AddWithValue("@sp", systemPrompt);
        cmd.Parameters.AddWithValue("@ua", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@id", conversationId);
        cmd.ExecuteNonQuery();
    }

    public void DeleteConversation(string conversationId)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "DELETE FROM conversations WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", conversationId);
        cmd.ExecuteNonQuery();
        Logger.Debug($"Deleted conversation: {conversationId}");
    }

    public List<ChatMessage> GetMessages(string conversationId)
    {
        var messages = new List<ChatMessage>();
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT id, conversation_id, role, content, timestamp FROM messages WHERE conversation_id = @cid ORDER BY id ASC";
        cmd.Parameters.AddWithValue("@cid", conversationId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            messages.Add(new ChatMessage
            {
                Id = reader.GetInt64(0),
                ConversationId = reader.GetString(1),
                Role = reader.GetString(2),
                Content = reader.GetString(3),
                Timestamp = DateTime.Parse(reader.GetString(4))
            });
        }
        return messages;
    }

    public void SaveMessage(ChatMessage message)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO messages (conversation_id, role, content, timestamp)
            VALUES (@cid, @role, @content, @ts)";
        cmd.Parameters.AddWithValue("@cid", message.ConversationId);
        cmd.Parameters.AddWithValue("@role", message.Role);
        cmd.Parameters.AddWithValue("@content", message.Content);
        cmd.Parameters.AddWithValue("@ts", message.Timestamp.ToString("O"));
        cmd.ExecuteNonQuery();

        // Update conversation timestamp
        using var cmd2 = _db.CreateCommand();
        cmd2.CommandText = "UPDATE conversations SET updated_at = @ua WHERE id = @id";
        cmd2.Parameters.AddWithValue("@ua", DateTime.UtcNow.ToString("O"));
        cmd2.Parameters.AddWithValue("@id", message.ConversationId);
        cmd2.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _db?.Close();
        _db?.Dispose();
    }
}
