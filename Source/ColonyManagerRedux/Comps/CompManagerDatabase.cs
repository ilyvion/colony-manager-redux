// CompManagerDatabase.cs
// Copyright (c) 2025 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// Component for storing and transferring manager job data as a byte array.
/// </summary>
public class CompManagerDatabase : ThingComp
{
    private byte[]? jobTransferData;
#pragma warning disable CA1819 // Properties should not return arrays
    /// <summary>
    /// Gets or sets the job transfer data as a byte array.
    /// </summary>
    public byte[]? JobTransferData
    {
        get => jobTransferData;
        set => jobTransferData = value;
    }
#pragma warning restore CA1819 // Properties should not return arrays

    /// <summary>
    /// Gets the component properties for this manager database.
    /// </summary>
    public CompProperties_ManagerDatabase Props => (CompProperties_ManagerDatabase)props;

    /// <inheritdoc/>
    public override void PostExposeData()
    {
        base.PostExposeData();
        DataExposeUtility.LookByteArray(ref jobTransferData, "jobTransferData");
    }
}
