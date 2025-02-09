using System.Numerics;

namespace Veldrid.VirtualReality;

public readonly struct HmdPoseState(
    Matrix4x4 leftEyeProjection,
    Matrix4x4 rightEyeProjection,
    Vector3 leftEyePosition,
    Vector3 rightEyePosition,
    Quaternion leftEyeRotation,
    Quaternion rightEyeRotation
)
{
    public readonly Matrix4x4 LeftEyeProjection = leftEyeProjection;
    public readonly Matrix4x4 RightEyeProjection = rightEyeProjection;
    public readonly Vector3 LeftEyePosition = leftEyePosition;
    public readonly Vector3 RightEyePosition = rightEyePosition;
    public readonly Quaternion LeftEyeRotation = leftEyeRotation;
    public readonly Quaternion RightEyeRotation = rightEyeRotation;

    public Vector3 GetEyePosition(VREye eye)
    {
        return eye switch
        {
            VREye.Left => LeftEyePosition,
            VREye.Right => RightEyePosition,
            _ => throw new VeldridException($"Invalid {nameof(VREye)}: {eye}."),
        };
    }

    public Quaternion GetEyeRotation(VREye eye)
    {
        return eye switch
        {
            VREye.Left => LeftEyeRotation,
            VREye.Right => RightEyeRotation,
            _ => throw new VeldridException($"Invalid {nameof(VREye)}: {eye}."),
        };
    }

    public Matrix4x4 CreateView(VREye eye, Vector3 positionOffset, Vector3 forward, Vector3 up)
    {
        Vector3 eyePos = GetEyePosition(eye) + positionOffset;
        Quaternion eyeQuat = GetEyeRotation(eye);
        Vector3 forwardTransformed = Vector3.Transform(forward, eyeQuat);
        Vector3 upTransformed = Vector3.Transform(up, eyeQuat);
        return Matrix4x4.CreateLookAt(eyePos, eyePos + forwardTransformed, upTransformed);
    }
}
