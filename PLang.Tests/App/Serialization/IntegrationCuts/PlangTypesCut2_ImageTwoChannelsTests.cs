namespace PLang.Tests.App.Serialization.IntegrationCuts;

// plang-types — Integration cut 2: same value, two channels, two wire shapes.
// One image instance, driven through two writers — text and json — gives a path
// placeholder and a base64 string respectively. The channel never branches on type;
// the type never knows about channels. The bridge is IWriter.Format.

public class PlangTypesCut2_ImageTwoChannelsTests
{
    [Test] public async Task SameImage_TextWriter_GivesPathPlaceholder()
        => throw new global::System.NotImplementedException();

    [Test] public async Task SameImage_JsonWriter_GivesBase64String()
        => throw new global::System.NotImplementedException();

    [Test] public async Task SameInstance_TwoWriters_NeverReMaterializesValue()
        => throw new global::System.NotImplementedException();

    [Test] public async Task ChannelSwitch_AcrossTwoOutputs_NoTypeBranching_InChannelCode()
        => throw new global::System.NotImplementedException();

    [Test] public async Task ImageInstance_DataTypeStaysImage_AcrossBothChannels()
        => throw new global::System.NotImplementedException();
}
