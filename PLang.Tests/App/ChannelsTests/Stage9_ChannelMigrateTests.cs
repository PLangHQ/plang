using GoalChannel = global::App.Channels.Channel.Goal.@this;
using EngineGoal = global::App.Goals.Goal.@this;
using MigrationEnvelope = global::App.Channels.Channel.MigrationEnvelope;
using GoalMigrationPayload = global::App.Channels.Channel.Goal.GoalMigrationPayload;

namespace PLang.Tests.App.ChannelsTests;

// Stage 9 — channel.migrate: API surface only, transport deferred.

public class Stage9_ChannelMigrateTests
{
    [Test]
    public async Task Migrate_OnSessionChannel_ReturnsMigrationEnvelope()
    {
        var app = new global::App.@this("/tmp/s9a");
        var ch = StreamChannel.Memory("chat");
        app.User.Channels.Register(ch);

        var result = await ch.Migrate();
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsTypeOf<MigrationEnvelope>();
    }

    [Test]
    public async Task MigrationEnvelope_Contains_NameRoleDirectionConfig()
    {
        var app = new global::App.@this("/tmp/s9b");
        var ch = new StreamChannel("audit", new MemoryStream(), ChannelDirection.Output, ownsStream: true)
        {
            Role = ChannelRole.Output,
            Buffer = 65536L,
            Timeout = TimeSpan.FromMinutes(2),
            Mime = "application/json",
            Encoding = "utf-8"
        };
        app.User.Channels.Register(ch);

        var result = await ch.Migrate();
        var env = (MigrationEnvelope)result.Value!;
        await Assert.That(env.Name).IsEqualTo("audit");
        await Assert.That(env.Role).IsEqualTo(ChannelRole.Output);
        await Assert.That(env.Direction).IsEqualTo(ChannelDirection.Output);
        await Assert.That(env.Config.Buffer).IsEqualTo(65536L);
        await Assert.That(env.Config.Timeout).IsEqualTo(TimeSpan.FromMinutes(2));
        await Assert.That(env.Config.Mime).IsEqualTo("application/json");
        await Assert.That(env.Config.Encoding).IsEqualTo("utf-8");
    }

    [Test]
    public async Task MigrationEnvelope_IsSignedBySourceSystemIdentity()
    {
        var app = new global::App.@this("/tmp/s9c");
        app.System.Identity = new global::App.modules.identity.Identity { Name = "sys-identity", PublicKey = "pk" };
        var ch = StreamChannel.Memory("chat");
        app.User.Channels.Register(ch);

        var result = await ch.Migrate();
        var env = (MigrationEnvelope)result.Value!;
        await Assert.That(env.Signature.IdentityName).IsEqualTo("sys-identity");
        await Assert.That(env.Signature.PublicKey).IsEqualTo("pk");
    }

    [Test]
    public async Task MigrationEnvelope_Signature_IsVerifiable()
    {
        var app = new global::App.@this("/tmp/s9d");
        var ch = StreamChannel.Memory("chat");
        app.User.Channels.Register(ch);
        var result = await ch.Migrate();
        var env = (MigrationEnvelope)result.Value!;

        await Assert.That(global::App.Channels.Channel.@this.Verify(env)).IsTrue();

        // Tamper: change Name → signature no longer verifies.
        var tampered = new MigrationEnvelope
        {
            Name = "chat-tampered",
            Role = env.Role,
            Direction = env.Direction,
            Config = env.Config,
            Payload = env.Payload,
            Signature = env.Signature
        };
        await Assert.That(global::App.Channels.Channel.@this.Verify(tampered)).IsFalse();
    }

    [Test]
    public async Task ConsoleStreamBackedChannel_Migrate_ReturnsNotMigratable()
    {
        var app = new global::App.@this("/tmp/s9e");
        // Console.OpenStandardOutput returns a non-MemoryStream — not migratable.
        var ch = new StreamChannel("output", Console.OpenStandardOutput(),
            ChannelDirection.Output, ownsStream: false);
        app.User.Channels.Register(ch);

        var result = await ch.Migrate();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("NotMigratable");
    }

    [Test]
    public async Task MemoryStreamBackedChannel_Migrate_ProducesCompleteEnvelope()
    {
        var app = new global::App.@this("/tmp/s9f");
        var ms = new MemoryStream();
        ms.Write(global::System.Text.Encoding.UTF8.GetBytes("payload-bytes"));
        var ch = new StreamChannel("buf", ms, ChannelDirection.Output, ownsStream: true);
        app.User.Channels.Register(ch);

        var result = await ch.Migrate();
        var env = (MigrationEnvelope)result.Value!;
        var bytes = (byte[])env.Payload!;
        await Assert.That(global::System.Text.Encoding.UTF8.GetString(bytes)).IsEqualTo("payload-bytes");
    }

    [Test]
    public async Task GoalChannel_Migrate_EnvelopeIncludesGoalNameAndVariablesSnapshot()
    {
        var app = new global::App.@this("/tmp/s9g");
        app.User.Context.Variables.Set("greeting", "hello");
        var goal = new EngineGoal { Name = "Logger", Path = "L.goal", PrPath = "/L.pr" };
        var ch = new GoalChannel("logger", goal, app.User);

        var result = await ch.Migrate();
        var env = (MigrationEnvelope)result.Value!;
        var payload = (GoalMigrationPayload)env.Payload!;
        await Assert.That(payload.GoalName).IsEqualTo("Logger");
        await Assert.That(payload.Variables).IsNotNull();
    }

    [Test]
    public async Task ChannelThis_FromMigration_PresentButThrowsNotImplemented()
    {
        var env = new MigrationEnvelope
        {
            Name = "any",
            Role = ChannelRole.Output,
            Direction = ChannelDirection.Output,
            Config = new global::App.Channels.Channel.ChannelConfigSnapshot(),
            Signature = new global::App.Channels.Channel.Signature
            {
                IdentityName = "x", PublicKey = "y", Bytes = new byte[0]
            }
        };
        await Assert.That(() => global::App.Channels.Channel.@this.FromMigration(env))
            .Throws<NotImplementedException>();
    }
}
