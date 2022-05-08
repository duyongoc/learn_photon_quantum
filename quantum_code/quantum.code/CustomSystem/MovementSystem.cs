

using Photon.Deterministic;

namespace Quantum
{

    public unsafe struct PlayerMovementFilter
    {
        public EntityRef entityRef;
        public PlayerId* playerId;
        public Transform3D* transform;
        public CharacterController3D* kcc;
    }

    unsafe class MovementSystem : SystemMainThreadFilter<PlayerMovementFilter>
    {

        public override void Update(Frame f, ref PlayerMovementFilter filter)
        {
            var input = f.GetPlayerInput(filter.playerId->PlayerRef);
            //Log.Info($"Update {input.}");

            var inputVector = new FPVector3(input->moveHorizontal, FP._0, input->moveVertical);
            var movementVector = filter.transform->Rotation * inputVector;

            //var movementAcceleration = FPVector2.Dot(filter.transform->Forward.XZ.Normalized, movementVector.XZ);
            //var forwardVelocity = FPMath.Abs(movementAcceleration);

            //Log.Info($"inputVector {inputVector}");
            //Log.Info($"forwardVelocity {forwardVelocity}");

            filter.kcc->Move(f, filter.entityRef, inputVector * 5);
        }

    }
}
