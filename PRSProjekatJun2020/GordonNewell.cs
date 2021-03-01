using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;

namespace PRSProjekatJun2020
{
    class GordonNewell
    {
        private int k ;

        private Matrix<double> P ;

        public Matrix<double> Probabilities { get{ return P; } }

        private List<int> S ;

        public StreamWriter Output { get; }

        private Matrix<double> Ik ;

        private Vector<double> B ;

        private List<bool> XisValid ;

        private int n { get; }

        private int Kmin = 2;
        private int Kmax = 8;

        public void FillP(int k)
        {
            P = Matrix<double>.Build.Dense(n + k, n + k);
            int i, j;
            for (i = 0; i < n; i++) P[0, i] = 0.1;
            for (i = 1; i < n + k; i++)
                for (j = 0; j < n; j++)
                    P[i, j] = (j == 0 ? (i < n ? 0.4 : 1) : 0);

            double UserDiskProbability = 0.6 / k;
            for (i = 0; i < n + k; i++)
                for (j = n; j < n + k; j++)
                    P[i, j] = (i < n ? UserDiskProbability : 0);
        }

        private Matrix<double> Xs ;
        /*
         * Matricna jednacina ce biti oblika A*mX=B
         * mX ce sadrzati promenljive od x2 do xk (x1 = 1) pomnozene za mi
         * Matrica(vektor) B ce sadrzati koeficijente uz x1 
         */
        public GordonNewell(List<int> S, string FileName)
        {
            n = 4;
            this.S = S;
            Output = new StreamWriter(FileName);
            Xs = Matrix<double>.Build.Dense(Kmax - Kmin + 1, n + Kmax);
            XisValid = new List<bool>(Kmax - Kmin + 1);
            for (int i = 0; i < Kmax - Kmin + 1; i++) XisValid.Add(false);
        }

        public Vector<double> compute(int k)
        {
            Ik = Matrix<double>.Build.DenseIdentity(n + k);
            B = Vector<double>.Build.Dense(n + k, 0);
            FillP(k);

            var A = P.Transpose() - Ik;
            B = -A.Column(0);
            A = A.RemoveColumn(0);

            Vector<double> mX = A.Solve(B);

            for (int i = 0; i < n + k; i++)
            {
                if (i == 0)
                {
                    Xs[k - Kmin, i] = 1;
                    continue;
                }
                Xs[k - Kmin, i] = (mX[i - 1] *= S[i]/S[0]); // m = 1/si; xi = mXi/m => xi = mXi*si/s0;
            }
            XisValid[k-Kmin] = true;
            return Xs.Row(k-Kmin);
        }

        public Vector<double> getX(int K)
        {
            if (K < Kmin || K > Kmax) throw new Exception("Incorrect parameter: k\nValid values are from 2 to 8.\n");
            k = K;
            if (!XisValid[k-Kmin]) return compute(k);
            else return Xs.Row(k-Kmin);
        }

        public void Flush()
        {
            Output.Write("K\tCPU\t\tSD1\t\tSD1\t\tSD2\t\tUD1\t\tUD2");
            for (int i = 3; i <= k; i++) Output.Write("\t\tSD{0}", i);
            Output.WriteLine("\n===================================================================================================");

            for (int i = Kmin; i <= Kmax; i++)
            {
                Output.Write("{0}\t", i);
                foreach (double tmp in Xs.Row(i - Kmin))
                    Output.Write("{0:0.000}\t", Math.Round(tmp, 3));
                Output.WriteLine();
            }
        }

        public void Clean()
        {
            Output.Close();
        }
    }
}
