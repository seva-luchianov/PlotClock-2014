using System;
using System.Threading;

using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;

using GHIElectronics.NETMF.FEZ;
using GHIElectronics.NETMF.Hardware;
using GHIElectronics.NETMF.System;

namespace PlotClockV1
{
    public class Program
    {
        static bool CALIBRATION = false;      // enable calibration mode
        static bool SETTIME = false;

        // When in calibration mode, adjust the following factor until the servos move exactly 90 degrees
        static double LEFTSERVOCONST = 650;
        static double RIGHTSERVOCONST = 650;

        // Zero-position of left and right servo
        // When in calibration mode, adjust the NULL-values so that the servo arms are at all times parallel
        // either to the X or Y axis
        static double SERVOLEFTNULL = 2500;
        static double SERVORIGHTNULL = 850;

        // lift positions of lifting servo
        static double LIFT0 = 1800; // on drawing surface
        static double LIFT1 = 1600; // between numbers
        static double LIFT2 = 1350; // going towards sweeper
        static double LIFT3 = 1740; //in sweeper

        // speed of liftimg arm, higher is slower
        static int LIFTSPEED = 2;

        // length of arms
        static double L1 = 35f;
        static double L2 = 55.1f;
        static double L3 = 13.2f;

        // origin points of left and right servo 
        static double O1X = 22f;
        static double O1Y = -25f;
        static double O2X = 47f;
        static double O2Y = -25f;
        static PWM servo1 = new PWM((PWM.Pin)FEZ_Pin.PWM.Di8);//lift servo
        static PWM servo2 = new PWM((PWM.Pin)FEZ_Pin.PWM.Di10);//left servo
        static PWM servo3 = new PWM((PWM.Pin)FEZ_Pin.PWM.Di9);//right servo
        static OutputPort led = new OutputPort((Cpu.Pin)FEZ_Pin.Digital.LED, true);
        //used to show status, solid light means working
        //single blink, no clock detected
        static int last_min = -1;

        static int servoLift = (int)LIFT2;
        static double sweepPosX = 69.5;
        static double sweepPosY = 42;
        static double armDiff = 9; //how much arms adjust in the Y direction as the marker is inserted into the sweeper.
        static double lastX = sweepPosX;
        static double lastY = sweepPosY;
        static int xShift = 7;
        static DS1307 clock = new DS1307();

        public static void Main()
        {
            if (SETTIME)
            {
                //adjust these accordingly
                int year = 2014;
                int month = 6;
                int day = 3;
                int hour = 14;
                int minute = 16;
                int second = 0;
                try
                {
                    clock.Set(new DateTime(year, month, day, hour, minute, second, 0));
                    clock.Dispose();
                    led.Write(!led.Read());
                }
                catch (Exception) //no clock detected
                {
                    while (true)
                    {
                        led.Write(!led.Read());
                        Thread.Sleep(1000);
                    }
                }
            }
            else //excecute normal program;
            {
                //Test if clock is connected
                try
                {
                    Debug.Print("Polling");
                    Utility.SetLocalTime(clock.Get());
                    clock.Dispose();
                    Debug.Print(System.DateTime.Now.ToString());
                }
                catch (Exception)
                {
                    while (true) //no clock detected
                    {
                        led.Write(!led.Read());
                        Thread.Sleep(1000);
                    }
                }
                //move to starting location

                int i;
                if (CALIBRATION)
                {
                    while (true)
                    {
                        // Servohorns will have 90° between movements, parallel to x and y axis
                        drawTo(-3, 29.2);
                        Thread.Sleep(500);
                        drawTo(74.1, 28);
                        Thread.Sleep(500);
                    }
                }
                else
                {
                    InsertIntoSweep();
                    while (true)
                    {
                        if (last_min != System.DateTime.Now.Minute)
                        {
                            number(3 + xShift, 3, 111, 1);
                            lift(LIFT2);
                            //first digit hours
                            i = 0;
                            while ((i + 1) * 10 <= System.DateTime.Now.Hour)
                            {
                                i++;
                            }
                            if (i != 0)
                            {
                                number(5 + xShift, 25, i, 0.9);
                            }
                            //second digit hours
                            number(19 + xShift, 25, (System.DateTime.Now.Hour - i * 10), 0.9);
                            number(27 + xShift, 25, 11, 0.9);

                            //find first digit mins
                            i = 0;
                            while ((i + 1) * 10 <= System.DateTime.Now.Minute)
                            {
                                i++;
                            }
                            number(31 + xShift, 23.5, i, 1);
                            //second digit mins
                            number(43 + xShift, 23.5, ((System.DateTime.Now.Minute) - i * 10), 1);

                            InsertIntoSweep();
                            Thread.Sleep(1000);
                            servo1.SetPulse(20 * 1000 * 1000, 0);
                            //servo1.Set(false);
                            servo2.Set(false);
                            servo3.Set(false);
                            last_min = System.DateTime.Now.Minute;
                        }
                        Thread.Sleep(500);
                    }

                }
            }
            Thread.Sleep(-1);
        }

        static void InsertIntoSweep()
        {
            lift(LIFT2);
            drawTo(sweepPosX, sweepPosY);
            //as servo moves down, arms move lower
            double liftDiff = LIFT3 - LIFT2;
            double ratio = armDiff / liftDiff;
            double iter = 0;
            while (servoLift <= LIFT3)
            {
                iter++;
                servoLift++;
                servo1.SetPulse(20 * 1000 * 1000, (uint)(servoLift * 1000));
                if (iter % 50 == 0)
                {
                    drawTo(sweepPosX, sweepPosY - ((servoLift - LIFT2) * ratio) + armDiff);
                }
                Thread.Sleep(LIFTSPEED);
            }
            //arms should now be at lift0
        }

        // Writing numeral with bx by being the bottom left originpoint. Scale 1 equals a 20 mm high font.
        // The structure follows this principle: move to first startpoint of the numeral, lift down, draw numeral, lift up
        static void number(double bx, double by, int num, double scale)
        {
            switch (num)
            {
                case 0:
                    drawTo(bx + 12 * scale, by + 6 * scale);
                    lift(LIFT0);
                    traceCounterClockwise(bx + 7 * scale, by + 10 * scale, 10 * scale, -0.8, 6.7, 0.5);
                    lift(LIFT1);
                    break;
                case 1:
                    //drawTo(bx + 3 * scale, by + 15 * scale);
                    //lift(LIFT0);
                    //drawTo(bx + 10 * scale, by + 20 * scale);
                    //drawTo(bx + 10 * scale, by + 0 * scale);
                    //lift(LIFT1);
                    //break;
                    drawTo(bx + 8 * scale, by + 20 * scale);
                    lift(LIFT0);
                    drawTo(bx + 8 * scale, by + 0 * scale);
                    lift(LIFT1);
                    break;
                case 2:
                    drawTo(bx + 2 * scale, by + 12 * scale);
                    lift(LIFT0);
                    traceClockwise(bx + 8 * scale, by + 14 * scale, 6 * scale, 3, -0.8, 1);
                    drawTo(bx + 1 * scale, by + 0 * scale);
                    drawTo(bx + 12 * scale, by + 0 * scale);
                    lift(LIFT1);
                    break;
                case 3:
                    drawTo(bx + 2 * scale, by + 17 * scale);
                    lift(LIFT0);
                    traceClockwise(bx + 5 * scale, by + 15 * scale, 5 * scale, 3, -2, 1);
                    traceClockwise(bx + 5 * scale, by + 5 * scale, 5 * scale, 1.57, -3, 1);
                    lift(LIFT1);
                    break;
                case 4:
                    /*drawTo(bx + 10 * scale, by + 0 * scale);
                    lift(LIFT0);
                    drawTo(bx + 10 * scale, by + 20 * scale);
                    drawTo(bx + 2 * scale, by + 6 * scale);
                    drawTo(bx + 12 * scale, by + 6 * scale);
                    lift(LIFT1);*/
                    drawTo(bx + 2 * scale, by + 20 * scale);
                    lift(LIFT0);
                    drawTo(bx + 2 * scale, by + 8 * scale);
                    drawTo(bx + 12 * scale, by + 8 * scale);
                    drawTo(bx + 12 * scale, by + 20 * scale);
                    drawTo(bx + 12 * scale, by + 0 * scale);
                    lift(LIFT1);
                    break;
                case 5:
                    drawTo(bx + 2 * scale, by + 5 * scale);
                    lift(LIFT0);
                    traceCounterClockwise(bx + 5 * scale, by + 6 * scale, 6 * scale, -2.5, 2, 1);
                    drawTo(bx + 5 * scale, by + 20 * scale);
                    drawTo(bx + 12 * scale, by + 20 * scale);
                    lift(LIFT1);
                    break;
                case 6:
                    drawTo(bx + 2 * scale, by + 10 * scale);
                    lift(LIFT0);
                    traceClockwise(bx + 7 * scale, by + 6 * scale, 6 * scale, 2, -4.4, 1);
                    drawTo(bx + 11 * scale, by + 20 * scale);
                    lift(LIFT1);
                    break;
                case 7:
                    drawTo(bx + 2 * scale, by + 20 * scale);
                    lift(LIFT0);
                    drawTo(bx + 12 * scale, by + 20 * scale);
                    drawTo(bx + 2 * scale, by + 0);
                    lift(LIFT1);
                    break;
                case 8:
                    drawTo(bx + 5 * scale, by + 10 * scale);
                    lift(LIFT0);
                    traceClockwise(bx + 5 * scale, by + 15 * scale, 5 * scale, 4.7, -1.6, 1);
                    traceCounterClockwise(bx + 5 * scale, by + 5 * scale, 5 * scale, -4.7, 2, 1);
                    lift(LIFT1);
                    break;

                case 9:
                    drawTo(bx + 9 * scale, by + 11 * scale);
                    lift(LIFT0);
                    traceClockwise(bx + 7 * scale, by + 15 * scale, 5 * scale, 4, -0.5, 1);
                    drawTo(bx + 5 * scale, by + 0);
                    lift(LIFT1);
                    break;
                case 111:
                    drawTo(60, sweepPosY);
                    lift(LIFT0);

                    drawTo(10, 45);
                    drawTo(10, 40);
                    drawTo(65, 40);
                    drawTo(65, 45);

                    drawTo(10, 40);
                    drawTo(10, 35);
                    drawTo(65, 35);
                    drawTo(65, 30);

                    drawTo(10, 40);
                    drawTo(10, 35);
                    drawTo(65, 35);
                    drawTo(65, 30);

                    drawTo(10, 30);
                    drawTo(10, 25);
                    drawTo(65, 25);
                    drawTo(65, 20);
                    drawTo(10, 20);

                    //drawTo(10, 20);
                    //drawTo(65, 20);
                    //drawTo(65, 15);
                    //drawTo(10, 15);

                    drawTo(60, sweepPosY);
                    drawTo(sweepPosX + 6, sweepPosY);
                    drawTo(sweepPosX + 3, sweepPosY);
                    break;

                case 11:
                    drawTo(bx + 5 * scale, by + 15 * scale);
                    lift(LIFT0);
                    traceCounterClockwise(bx + 5 * scale, by + 15 * scale, 0.1 * scale, 1, -1, 1);
                    lift(LIFT1);
                    drawTo(bx + 5 * scale, by + 5 * scale);
                    lift(LIFT0);
                    traceCounterClockwise(bx + 5 * scale, by + 5 * scale, 0.1 * scale, 1, -1, 1);
                    lift(LIFT1);
                    break;

            }
        }

        static void lift(double lift)
        {
            if (servoLift >= lift)
            {
                while (servoLift >= lift)
                {
                    servoLift--;
                    servo1.SetPulse(20 * 1000 * 1000, (uint)(servoLift * 1000));
                    Thread.Sleep(LIFTSPEED);
                }
            }
            else
            {
                while (servoLift <= lift)
                {
                    servoLift++;
                    servo1.SetPulse(20 * 1000 * 1000, (uint)(servoLift * 1000));
                    Thread.Sleep(LIFTSPEED);

                }
            }
        }


        static void traceClockwise(double bx, double by, double radius, double start, double ende, double sqee)
        {
            double inkr = -0.05f;
            double count = 0;
            do
            {
                drawTo(sqee * radius * MathEx.Cos(start + count) + bx, radius * MathEx.Sin(start + count) + by);
                count += inkr;
            }
            while ((start + count) > ende);

        }

        static void traceCounterClockwise(double bx, double by, double radius, double start, double ende, double sqee)
        {
            double inkr = 0.05f;
            double count = 0;

            do
            {
                drawTo(sqee * radius * MathEx.Cos(start + count) + bx, radius * MathEx.Sin(start + count) + by);
                count += inkr;
            }
            while ((start + count) <= ende);
        }

        static void drawTo(double pX, double pY)
        {
            double dx, dy, c;
            int i;

            // dx dy of new point
            dx = pX - lastX;
            dy = pY - lastY;
            //path length in mm, times 4 equals 4 steps per mm
            c = MathEx.Floor(4 * MathEx.Sqrt(dx * dx + dy * dy));

            if (c < 1) c = 1;

            for (i = 0; i <= c; i++)
            {
                // draw line point by point
                set_XY(lastX + (i * dx / c), lastY + (i * dy / c));
            }

            lastX = pX;
            lastY = pY;
        }

        static double return_angle(double a, double b, double c)
        {
            // cosine rule for angle between c and a
            return MathEx.Acos((a * a + c * c - b * b) / (2 * a * c));
        }

        static void set_XY(double Tx, double Ty)
        {
            Thread.Sleep(1);
            double dx, dy, c, a1, a2, Hx, Hy;

            // calculate triangle between pen, servoLeft and arm joint
            // cartesian dx/dy
            dx = Tx - O1X;
            dy = Ty - O1Y;

            // polar lemgth (c) and angle (a1)
            c = System.Math.Pow(dx * dx + dy * dy, 0.5); // 
            a1 = MathEx.Atan2(dy, dx); //
            a2 = return_angle(L1, L2, c);

            servo2.SetPulse(20 * 1000 * 1000, (uint)(MathEx.Floor(((a2 + a1 - MathEx.PI) * LEFTSERVOCONST) + SERVOLEFTNULL)) * 1000);//bottom right for right servo

            // calculate joinr arm point for triangle of the right servo arm
            a2 = return_angle(L2, L1, c);
            Hx = Tx + L3 * MathEx.Cos((a1 - a2 + 0.621) + MathEx.PI); //36,5°
            Hy = Ty + L3 * MathEx.Sin((a1 - a2 + 0.621) + MathEx.PI);

            // calculate triangle between pen joint, servoRight and arm joint
            dx = Hx - O2X;
            dy = Hy - O2Y;

            c = System.Math.Pow(dx * dx + dy * dy, 0.5); // 
            a1 = MathEx.Atan2(dy, dx);
            a2 = return_angle(L1, (L2 - L3), c);

            //servo3.writeMicroseconds(MathEx.Floor(((a1 - a2) * SERVOFAKTORRIGHT) + SERVORIGHTNULL));
            servo3.SetPulse(20 * 1000 * 1000, (uint)(MathEx.Floor(((a1 - a2) * RIGHTSERVOCONST) + SERVORIGHTNULL)) * 1000);//bottom right for right servo
        }
    }
}
