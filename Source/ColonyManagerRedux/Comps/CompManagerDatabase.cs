// CompManagerDatabase.cs
// Copyright (c) 2025 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

public class CompManagerDatabase : ThingComp
{
    private byte[]? jobTransferData = null;
    public byte[]? JobTransferData { get => jobTransferData; set => jobTransferData = value; }

    public CompProperties_ManagerDatabase Props => (CompProperties_ManagerDatabase)props;

    public override void PostExposeData()
    {
        base.PostExposeData();
        DataExposeUtility.LookByteArray(ref jobTransferData, "jobTransferData");
    }
}