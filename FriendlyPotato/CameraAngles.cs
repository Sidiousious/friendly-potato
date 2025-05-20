using System;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

namespace FriendlyPotato;

public static class CameraAngles
{
    public static double AngleToTarget(Vector3 pos, double aimAngle)
    {
        var dirVector3 = FriendlyPotato.ClientState.LocalPlayer!.Position - pos;
        var dirVector = Vector2.Normalize(new Vector2(dirVector3.X, dirVector3.Z));
        var dirAngle = AimAngle(dirVector);
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

    public static double OwnAimAngle()
    {
        return AimAngle(OwnAimVector2());
    }

    public static double AimAngle(Vector2 aimVector)
    {
        return Math.Atan2(aimVector.Y, aimVector.X) * 180f / Math.PI;
    }

    public static unsafe Vector2 OwnAimVector2()
    {
        try
        {
            var camera = CameraManager.Instance()->CurrentCamera;
            var threeDAim =
                new Vector3(camera->RenderCamera->Origin.X, camera->RenderCamera->Origin.Y,
                            camera->RenderCamera->Origin.Z) - FriendlyPotato.ClientState.LocalPlayer!.Position;
            return Vector2.Normalize(new Vector2(threeDAim.X, threeDAim.Z));
        }
        catch (NullReferenceException)
        {
            // Camera does not exist during loading screens
            return Vector2.Zero;
        }
    }
}
