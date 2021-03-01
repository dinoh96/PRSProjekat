using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Random;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PRSProjekatJun2020
{

    public class Node
    {
        public int Server;
        public double Time;
        public bool isStart;

        public Node(int Server, double Time, bool isStart)
        {
            this.Server = Server;
            this.Time = Time;
            this.isStart = isStart;
        }

        public Node(int Server, bool isStart)
        {
            this.Server = Server;
            this.isStart = isStart;
            Time = -1;
        }
    }
    class Simulation
    {

        private double endTime;
        private long TimeInMiliSeconds;
        private int Kmin;
        private int Kmax;
        private int fixedServers;

        private int jobs;
        private int k;
        private int DeviceNumber;
        private int numOfRepetitions;

        private List<int> Queue;

        private List<double> X;
        private List<double> U;
        private List<double> N;
        private List<double> Jobs;
        public double lastUpdatedTime;

        //private List<double> LastUpdate;

        private List<double> XPrintFirst;
        private List<double> UPrintFirst;
        private List<double> JobsPrintFirst;
        private List<double> NPrintFirst;
        private double SystemResponseFirst;

        private List<double> XPrint;
        private List<double> UPrint;
        private List<double> JobsPrint;
        private List<double> NPrint;
        private double SystemResponse;

        private List<double> XBuzen;
        private List<double> UBuzen;
        private List<double> JobsBuzen;
        private List<double> NBuzen;
        private double SystemResponseBuzen;

        public LinkedList<Node> execList = null;
        private List<double> StartTimes;
        private Random randomGenerator = new Random(DateTime.Now.Millisecond);
        private double currentTime;

        public static int cnt = 0;

        public static double[,] mat = new double[12, 12];
        public static int[] numberOfJobs = new int[12];

        public List<int> S { get; }
        public StreamWriter Output { get; }
        public StreamWriter OutputMean { get; }
        public StreamWriter OutputDeviation { get; }
        public StreamWriter OutputDeviationMean { get; }

        private Matrix<double> P;
        private int BottleNeck;
        public List<int> SimulationParameters { get; set; }

        public static double[] processTimes = new double[12];
        public static int[] processJobs = new int[12];

        static Simulation()
        {
            for (int i = 0; i < 12; processTimes[i++] = 100) ;
        }

        public Simulation(List<int> S, int jobs, int numOfRepetitions, StreamWriter FileName, StreamWriter FileNameMean, StreamWriter FileNameDeviation, StreamWriter FileNameDeviationMean, int TimeInMinutes = 24 * 60, int fixedServers = 4, int Kmin = 2, int Kmax = 8)
        {
            endTime = TimeInMinutes * 60 * 1000;

            this.S = S;
            Output = FileName;
            OutputMean = FileNameMean;
            OutputDeviation = FileNameDeviation;
            OutputDeviationMean = FileNameDeviationMean;
            this.fixedServers = fixedServers;
            this.Kmin = Kmin;
            this.Kmax = Kmax;
            this.jobs = jobs;
            this.numOfRepetitions = numOfRepetitions;
        }


        public int CalculateProcessDuration(int to)
        {
            int tmp = (int)Math.Floor(-Math.Log(1.0 - randomGenerator.NextDouble()) * S[to]);
            Simulation.processTimes[to] += tmp;
            Simulation.processJobs[to] += 1;

            return tmp;
        }

        public int CalculateNextServer(int from)
        {
            int to = 0;
            double randomNumber = randomGenerator.NextDouble();
            for (; to < DeviceNumber && randomNumber > 0; to++)
                randomNumber -= P[from, to];
            if (to != 0) to--;
            Simulation.mat[from, to]++;
            Simulation.numberOfJobs[from]++;

            return to;
        }

        public void Insert(Node node)
        {
            Node before = null;
            try
            {
                before = execList.Last(tmp => tmp.Time <= node.Time);
            }
            catch { }


            if (before == null)
                execList.AddFirst(node);
            else
                execList.AddAfter(execList.Find(before), node);
        }

        public void SetB(Buzen B)

        {
            UBuzen = B.U.ToList<double>();
            XBuzen = B.X.ToList<double>();
            NBuzen = B.N.ToList<double>();
            SystemResponseBuzen = B.SystemResponse;
        }

        public void Init(Matrix<double> P, int k)
        {
            this.k = k;
            this.P = P;
            DeviceNumber = fixedServers + k;

            XPrintFirst = new List<double>(DeviceNumber);
            UPrintFirst = new List<double>(DeviceNumber);
            JobsPrintFirst = new List<double>(DeviceNumber);
            NPrintFirst = new List<double>(DeviceNumber);

            XPrint = new List<double>(DeviceNumber);
            UPrint = new List<double>(DeviceNumber);
            JobsPrint = new List<double>(DeviceNumber);
            NPrint = new List<double>(DeviceNumber);


            for (int i = 0; i < DeviceNumber; i++)
            {
                XPrintFirst.Add(0);
                UPrintFirst.Add(0);
                NPrintFirst.Add(0);
                JobsPrintFirst.Add(0);

                XPrint.Add(0);
                UPrint.Add(0);
                NPrint.Add(0);
                JobsPrint.Add(0);
            }
        }

        public void PrepareForRun()
        {
            currentTime = 0;
            lastUpdatedTime = 0;
            passed = 0;

            execList = new LinkedList<Node>();
            StartTimes = new List<double>(DeviceNumber);

            Queue = new List<int>(DeviceNumber);

            X = new List<double>(DeviceNumber);
            U = new List<double>(DeviceNumber);
            Jobs = new List<double>(DeviceNumber);
            N = new List<double>(DeviceNumber);
            //LastUpdate = new List<double>(DeviceNumber);


            Node tmp = null;
            for (int i = 0; i < jobs; i++)
            {

                tmp = new Node(0, (tmp == null ? 0 : (tmp.Time + 1)), true);
                execList.AddLast(tmp);
                tmp = new Node(0, tmp.Time + CalculateProcessDuration(0), false);
                execList.AddLast(tmp);
            }


            for (int i = 0; i < DeviceNumber; i++)
            {
                Queue.Add(i == 0 ? jobs : 0);
                X.Add(0);
                U.Add(0);
                N.Add(0);
                Jobs.Add(0);

                //LastUpdate.Add(0);
                StartTimes.Add(-1);
            }
        }

        public void Start()
        {
            PrepareForRun();
            Run();
            PrepareForWriting(true);
            Console.WriteLine("Passed: {0}", passed);

            for (int simIteration = 1; simIteration < numOfRepetitions; simIteration++)
            {
                PrepareForRun();
                Run();
                PrepareForWriting(false);
                Console.WriteLine("Passed: {0}", passed);
            }
        }

        public void Run()
        { 
            double a = U[0];
            while (currentTime < endTime)
            {
                LinkedListNode<Node> first = execList.First;
                execList.RemoveFirst();
                currentTime = first.Value.Time;

                if (first.Value.isStart && currentTime < endTime)
                {
                    // naisao na start node
                    ProcessStartNode(first.Value);
                }
                if (!first.Value.isStart)
                {
                    if (StartTimes[first.Value.Server] > 0)
                        // naisao na end node
                        ProcessEndNode(first.Value);
                }
            }
            double b = U[0];
            while (!execList.Count.Equals(0))
            {
                LinkedListNode<Node> first = execList.First;
                execList.RemoveFirst();
                
                if (first.Value.isStart)
                    continue;
                if (StartTimes[first.Value.Server] < 0)
                    continue;

                ProcessEndNode(first.Value, true);
            }
            double c = U[0];

            UpdateN(true);

            Simulation.cnt++;

            //Console.WriteLine("{0}\t{1}\t{2}\t{3}", a, b, c, U[0]);

        }

        public double passed = 0;
        public List<List<double>> QueuePrint = new List<List<double>>();
        public List<List<double>> ExecPrint = new List<List<double>>();


        public void UpdateN(bool simulationFinished = false)
        {
            double timePassed = ((simulationFinished ? endTime : currentTime) - lastUpdatedTime);

            //if (timePassed < 0)
            //    Console.WriteLine("{0}\t{1}\t{2}\t{3}", simulationFinished, currentTime, lastUpdatedTime, timePassed);
            List<double> tmp = new List<double>();
            List<double> tmp2 = new List<double>();
            tmp.Add(currentTime);
            for (int i = 0; i < N.Count; i++)
            {
                N[i] += timePassed * Queue[i];
                tmp.Add(Queue[i]);
                tmp2.Add(StartTimes[i]);
            }

            QueuePrint.Add(tmp);
            ExecPrint.Add(tmp2);

            passed += timePassed;

            lastUpdatedTime = currentTime;

        }

        public void print (StreamWriter file)
        {

            for (int i = 0; i < QueuePrint.Count; i++)
            {
                foreach (var server in QueuePrint[i])
                    if (server == QueuePrint[i].First())
                        file.Write("{0}:\t", server);
                    else
                        file.Write("{0}\t", server);
                file.Write("\t\t");

                foreach (var server in ExecPrint[i])
                    file.Write("{0}\t", server);
                file.WriteLine();
            }


        }

        public void ProcessStartNode(Node node)
        {
            StartTimes[node.Server] = node.Time;

            UpdateN();
            Queue[node.Server]--;

            Jobs[node.Server]++;
        }

        public void ProcessEndNode(Node node, bool simulationFinished = false)
        {
            double diff = (simulationFinished ? endTime : node.Time) - StartTimes[node.Server] + 1;
            if (diff < 0) Console.WriteLine("Holup");

            U[node.Server] += diff;
            N[node.Server] += diff;

            if (simulationFinished)
            {
                return;
            }

            int nextServer = CalculateNextServer(node.Server);

            Node newStart = new Node(nextServer, true);
            Node newEnd = new Node(nextServer, false);

            try
            {
                Node endOfTheLastJobOnServer = execList.Last(tmp => tmp.Server.Equals(nextServer) && !tmp.isStart);
                newStart.Time = Math.Floor(endOfTheLastJobOnServer.Time) + 1;
            }
            catch
            {
                newStart.Time = Math.Floor(node.Time) + 1;
            }

            newEnd.Time = newStart.Time + CalculateProcessDuration(nextServer);

            Insert(newStart);
            Insert(newEnd);

            UpdateN();
            Queue[nextServer]++;

            StartTimes[node.Server] = -1;
        }


        public void PrepareForWriting(bool firstSimulation = false)
        {
            double max = 0;

            for (int i = 0; i < DeviceNumber; i++)
            {

                UPrint[i] += U[i] / endTime;
                JobsPrint[i] += Jobs[i];
                XPrint[i] += Jobs[i] / endTime * 1000;
                NPrint[i] += N[i] / endTime;

                if (UPrint[i] > max)
                {
                    max = UPrint[i];
                    BottleNeck = i;
                }

                if (firstSimulation)
                {
                    UPrintFirst[i] = UPrint[i];
                    JobsPrintFirst[i] = JobsPrint[i];
                    XPrintFirst[i] = XPrint[i];
                    NPrintFirst[i] = NPrint[i];
                }

            }
            SystemResponse += (double)jobs / (Jobs[0] / endTime * 1000);

            if (firstSimulation)
                SystemResponseFirst = (double)jobs / XPrintFirst[0];
        }

        private void PrepareDeviation(bool firstSimulation = false)
        {
            if (firstSimulation)
            {
                for (int i = 0; i < DeviceNumber; i++)
                {
                    UPrintFirst[i] = Math.Abs(UPrintFirst[i] - UBuzen[i]) / UPrintFirst[i] * 100;
                    XPrintFirst[i] = Math.Abs(XPrintFirst[i] - XBuzen[i]) / XPrintFirst[i] * 100;
                    NPrintFirst[i] = Math.Abs(NPrintFirst[i] - NBuzen[i]) / NPrintFirst[i] * 100;
                }
                SystemResponseFirst = Math.Abs(SystemResponseFirst - SystemResponseBuzen) / SystemResponseFirst * 100;
            }
            else
            {
                for (int i = 0; i < DeviceNumber; i++)
                {
                    UPrint[i] = Math.Abs(UPrint[i] / numOfRepetitions - UBuzen[i]) / (UPrint[i] / numOfRepetitions) * 100;
                    XPrint[i] = Math.Abs(XPrint[i] / numOfRepetitions - XBuzen[i]) / (XPrint[i] / numOfRepetitions) * 100;
                    NPrint[i] = Math.Abs(NPrint[i] / numOfRepetitions - NBuzen[i]) / (NPrint[i] / numOfRepetitions) * 100;
                }
                SystemResponse = Math.Abs(SystemResponse / numOfRepetitions - SystemResponseBuzen) / (SystemResponse / numOfRepetitions) * 100;
            }
        }

        public void Flush(bool firstSimulation = false, bool diviation = false)
        {
            StreamWriter File = firstSimulation ? Output : OutputMean;
            List<double> PrintList;
            int factor = firstSimulation ? 1 : numOfRepetitions;

            if (diviation)
            {
                File = firstSimulation ? OutputDeviation : OutputDeviationMean;
                PrepareDeviation(firstSimulation);
            }


            File.WriteLine("K: {0}", k);

            File.Write("\t\tJobs: {0}\t", jobs);

            File.Write("\tCPU\t\tSD1\t\tSD2\t\tSD3\t\tUD1\t\tUD2");
            for (int i = 3; i <= k; i++) File.Write("\t\tSD{0}", i);
            File.Write("\n\t\t\t\t\t\t");

            for (int i = 0; i < DeviceNumber; i++) File.Write("========");
            File.WriteLine();

            PrintList = firstSimulation ? UPrintFirst : UPrint;

            File.Write("Iskoriscenje resursa\t");
            foreach (double tmp in PrintList)
                if (!diviation)
                    File.Write("{0:0.0000}\t", tmp / factor);
                else File.Write("{0:0.00}%\t", tmp);
            File.WriteLine();

            PrintList = firstSimulation ? XPrintFirst : XPrint;

            File.Write("Protoci kroz resurse\t");
            foreach (double tmp in PrintList)
                if (!diviation)
                    File.Write("{0:0.0000}\t", tmp / factor);
                else File.Write("{0:0.00}%\t", tmp);
            File.WriteLine();

            PrintList = firstSimulation ? NPrintFirst : NPrint;

            File.Write("Prosecan broj poslova\t");
            foreach (double tmp in PrintList)
                if (!diviation)
                    File.Write("{0:0.0000}\t", tmp / factor);
                else File.Write("{0:0.00}%\t", tmp);
            File.WriteLine();

            File.Write("Vreme odziva sistema\t");
            if (!diviation)
                File.WriteLine("{0:0.0000}", firstSimulation ? SystemResponseFirst : SystemResponse / factor);
            else File.Write("{0:0.00}%\t", firstSimulation ? SystemResponseFirst : SystemResponse);

            if (firstSimulation && !diviation)
            {
                File.Write("Kritican resurs sistema\t{0} - ", BottleNeck);
                string BottleNeckName;
                switch (BottleNeck)
                {
                    case 0:
                        BottleNeckName = "CPU";
                        break;
                    case 1:
                    case 2:
                    case 3:
                        BottleNeckName = "SD" + BottleNeck;
                        break;
                    case -1:
                        BottleNeckName = "Error, critical resource is not set.";
                        break;
                    default:
                        BottleNeckName = "UD" + (BottleNeck - 3).ToString();
                        break;
                }
                File.WriteLine("{0}\n", BottleNeckName);
            }
            else
                File.WriteLine("\n");

            for (int i = 0; i < DeviceNumber + 3; i++) File.Write("********");
            File.WriteLine("\n");
        }
        
    }
}
