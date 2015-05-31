using System;
using System.Globalization;

namespace UsrpIO.DataType
{
    /// <summary>
    /// This class represents complex number object
    /// containing real and the imaginary part of the number
    /// </summary>
    public struct Complex
    {
        public double mReal;
        public double mImag;

        /// <summary>
        /// This method allows to explicitly cast string object to the Complex object.
        /// </summary>
        /// <param name="pNumber">String containing real and imaginary values.</param>
        /// <returns>
        /// Complex object which contains values contained in string if it is properly formated
        /// and if data is inproperly formated then complex object has real and imaginary values
        /// equal to zero.
        /// </returns>
        public static explicit operator Complex(string pNumber)
        {
            Complex lCmplx = new Complex();
            string[] lNmbr = pNumber.Split(' ');
            try
            {
                if (lNmbr.Length == 2) // should have real and imag part
                {
                    lCmplx.mReal = Double.Parse(lNmbr[0], CultureInfo.InvariantCulture);
                    lCmplx.mImag = Double.Parse(lNmbr[1], CultureInfo.InvariantCulture);
                }
            }
            catch (Exception)
            {
                lCmplx.mReal = 0;
                lCmplx.mImag = 0;
            }

            return lCmplx;
        }

        /// <summary>
        /// This method returns Complex object description.
        /// </summary>
        /// <returns>Object real and imaginary values.</returns>
        public override string ToString()
        {
            return mReal + " " + mImag;
        }
    }
}
