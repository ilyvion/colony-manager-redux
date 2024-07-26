// CameraDriver_Extensions.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.Reflection;

namespace ColonyManagerRedux;

public static class CameraDriver_Extensions
{
    private static readonly FieldInfo CameraDriver_panner = AccessTools.Field(typeof(CameraDriver), "panner");
    public static bool IsPanning(this CameraDriver cameraDriver)
    {
        return ((CameraPanner)CameraDriver_panner.GetValue(cameraDriver)).Moving;
    }
}
