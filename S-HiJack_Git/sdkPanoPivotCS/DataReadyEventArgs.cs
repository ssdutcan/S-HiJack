using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HiJack
{
    public class DataReadyEventArgs : EventArgs
    {
        private Byte receiveData;

        public DataReadyEventArgs(Byte Data)
        {
            this.receiveData = Data;
        }

        public Byte ReceiveData
        {
            get { return this.receiveData; }
        }
    }
}
