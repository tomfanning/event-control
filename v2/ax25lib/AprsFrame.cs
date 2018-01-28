using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ax25lib
{
    public class AprsFrame
    {
        Ax25Frame ax25Frame;

        public AprsFrame(Ax25Frame ax25Frame)
        {
            this.ax25Frame = ax25Frame;

            // first byte of the ax25 info field is the data type ID
            // from the table on page 17 of http://www.aprs.org/doc/APRS101.PDF


        }
    }
}
