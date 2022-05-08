using System.Linq;
using Photon.Deterministic;

namespace Quantum
{
    public unsafe struct PlayerWeaponFilter
    {
        public EntityRef entityRef;
        public PlayerId* playerId;
        public Weapon* weapon;
    }

    public unsafe class AttackSystem : SystemMainThread
    {

        public override void Update(Frame f)
        {
            Log.Debug("Update");
            InputAttack(f);
        }


        private static void InputAttack(Frame f)
        {
            var players = f.Unsafe.FilterStruct<PlayerWeaponFilter>();
            var playerStruct = default(PlayerWeaponFilter);

            while (players.Next(&playerStruct))
            {
                var input = f.GetPlayerInput(playerStruct.playerId->PlayerRef);

                if (input->Attack.WasPressed)
                {
                    Log.Debug("bummm attack");
                }
            }
        }


    }

}
