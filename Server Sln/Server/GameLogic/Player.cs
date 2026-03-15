using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.GameLogic
{
    public enum EPlayerState
    {
        Offline,
        Online,
        InGame
    }

    public class Player
    {
        #region FIELDS

        private EPlayerState _state = EPlayerState.Offline;

        #endregion

        #region PROPERTIES  

        public EPlayerState State => _state;

        #endregion


        public Player() { } 
    }
}
