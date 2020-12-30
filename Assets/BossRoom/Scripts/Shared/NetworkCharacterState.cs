using System;
using System.IO;
using MLAPI;
using MLAPI.Messaging;
using MLAPI.NetworkedVar;
using UnityEngine;
using MLAPI.Serialization.Pooled;


namespace BossRoom
{



    /// <summary>
    /// Contains all NetworkedVars and RPCs of a character. This component is present on both client and server objects.
    /// </summary>
    public class NetworkCharacterState : NetworkedBehaviour
    {
        public NetworkedVarVector3 NetworkPosition;
        public NetworkedVarFloat NetworkRotationY;
        public NetworkedVarFloat NetworkMovementSpeed;

        public NetworkedVarInt HitPoints;
        public NetworkedVarInt Mana;

        /// <summary>
        /// Gets invoked when inputs are received from the client which own this networked character.
        /// </summary>
        public event Action<Vector3> OnReceivedClientInput;

        /// <summary>
        /// RPC to send inputs for this character from a client to a server.
        /// </summary>
        /// <param name="movementTarget">The position which this character should move towards.</param>
        [ServerRPC]
        public void SendCharacterInputServerRpc(Vector3 movementTarget)
        {
            OnReceivedClientInput?.Invoke(movementTarget);
        }


        // ACTION SYSTEM

        /// <summary>
        /// This event is raised on the server when an action request arrives, and on the client after the server
        /// has broadcast an action play.
        /// </summary>
        public event Action<BossRoom.ActionRequestData> DoActionEvent;

        /// <summary>
        /// Client->Server RPC that sends a request to play an action. 
        /// </summary>
        /// <param name="data">Data about which action to play an dits associated details. </param>
        public void C2S_DoAction(ref ActionRequestData data)
        {
            using (PooledBitStream stream = PooledBitStream.Get())
            {
                SerializeAction(ref data, stream);
                InvokeServerRpcPerformance(RecvDoActionServer, stream);
            }
        }

        /// <summary>
        /// Server->Client RPC that broadcasts this action play to all clients. 
        /// </summary>
        /// <param name="data">The data associated with this Action, including what action type it is.</param>
        public void S2C_BroadcastAction(ref ActionRequestData data )
        {
            using (PooledBitStream stream = PooledBitStream.Get())
            {
                SerializeAction(ref data, stream);
                InvokeClientRpcOnEveryonePerformance(RecvDoActionClient, stream);
            }
        }

        private void SerializeAction( ref ActionRequestData data, PooledBitStream stream )
        {
            var Logic = ActionDescriptionList.LIST[data.ActionTypeEnum][0].Logic;

            using (PooledBitWriter writer = PooledBitWriter.Get(stream))
            {
                writer.WriteInt16((short)data.ActionTypeEnum);
                if (Logic == ActionLogic.RANGED)
                {
                    writer.WriteVector3(data.Direction);
                }
                if (Logic == ActionLogic.RANGEDTARGETED)
                {
                    writer.WriteIntArray(data.TargetIds);
                }
            }
        }

        [ClientRPC]
        private void RecvDoActionClient(ulong clientId, Stream stream )
        {
            RecvDoAction(clientId, stream);

        }

        [ServerRPC]
        private void RecvDoActionServer(ulong clientId, Stream stream)
        {
            RecvDoAction(clientId, stream);
        }

        private void RecvDoAction(ulong clientId, Stream stream )
        {
            ActionRequestData data = new ActionRequestData();

            using (PooledBitReader reader = PooledBitReader.Get(stream))
            {
                data.ActionTypeEnum = (Action)reader.ReadInt16();

                var Logic = ActionDescriptionList.LIST[data.ActionTypeEnum][0].Logic;

                if( Logic == ActionLogic.RANGED )
                {
                    data.Direction = reader.ReadVector3();
                }
                if( Logic == ActionLogic.RANGEDTARGETED )
                {
                    data.TargetIds = reader.ReadIntArray(data.TargetIds);
                }
            }

            DoActionEvent?.Invoke(data);
        }


    }
}
