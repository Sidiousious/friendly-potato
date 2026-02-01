using System;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

namespace FriendlyPotato;

public static class CameraAngles
{
    public static double AngleToTarget(Vector3 pos, double aimAngle)
    {
        var dirVector3 = FriendlyPotato.LastPlayerPosition - pos;
        var dirVector = Vector2.Normalize(new Vector2(dirVector3.X, dirVector3.Z));
        var dirAngle = AimAngle(dirVector) - 90f;
        var angularDifference = dirAngle - aimAngle;
        switch (angularDifference)
        {
            case > 180:
                angularDifference -= 360;
                break;
            case < -180:
                angularDifference += 360;
                break;
        }

        return angularDifference;
    }

    public static double AimAngle(Vector2 aimVector)
    {
        return Math.Atan2(aimVector.Y, aimVector.X) * 180f / Math.PI;
    }

    public static unsafe float OwnCamAngle()
    {
        var camera = FFXIVClientStructs.FFXIV.Client.Game.Control.CameraManager.Instance()->GetActiveCamera();
        if ((nint)camera == 0)
        {
            // Camera does not exist during loading screens
            return 0;
        }
        return -(camera->ZoomMode == FFXIVClientStructs.FFXIV.Client.Game.CameraZoomMode.FirstPerson ? camera->DirH + MathF.PI : camera->DirH) * (180f / MathF.PI);
    }
}
