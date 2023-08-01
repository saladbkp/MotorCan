using ECAN;
using ECanTest;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;

namespace Motor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        #region DECLARE
        int running_func; // UI STATUS
        int count_round = 0; // motor round
        Stopwatch rtnTimeS; // return Start Timestamp 
        bool recording; // study cmd
        int taskdone = 0; // study task
        bool isaaply; // applying task?
        List<string> cmdslist; // save cmd list (thread wait/)

        string rtnOption;
        Dictionary<int,string> funcKeyValuePairs = new Dictionary<int, string>(); // UI convert

        // GCAN
        byte m_Baudrate;
        byte m_connect = 0;
        ComProc mCan = new ComProc();
        System.Windows.Threading.DispatcherTimer dispatcherTimer = new System.Windows.Threading.DispatcherTimer(); // read response
        #endregion

        public MainWindow()
        {
            InitializeComponent();
            cmd.AppendText(">");
            cmd.CaretPosition = cmd.CaretPosition.DocumentEnd;

            cbbBaudrates.SelectedIndex = 0;
            funcKeyValuePairs.Add(0, "NONE");
            funcKeyValuePairs.Add(1, "HOME");
            funcKeyValuePairs.Add(2, "STOP");
            funcKeyValuePairs.Add(3, "1 LEFT");
            funcKeyValuePairs.Add(4, "1 RIGHT");
            funcKeyValuePairs.Add(5, "5 CYCLE");

            dispatcherTimer.Tick += Timer_Tick;
            dispatcherTimer.Interval = TimeSpan.FromSeconds(1.0 / 60.0);
            dispatcherTimer.Start();
        }

        #region FUNC
        private string init(string m_Baudrate)
        {
            string rtn_text = "";
            if (m_connect == 1)
            {
                m_connect = 0;
                this.mCan.EnableProc = false;
                ECANDLL.CloseDevice(1, 0);

                stack.IsEnabled = false;
                return "";
            }

            INIT_CONFIG init_config = new INIT_CONFIG();

            init_config.AccCode = 0;
            init_config.AccMask = 0xffffff;
            init_config.Filter = 0;

            switch (m_Baudrate)
            {
                case "1000": //1000

                    init_config.Timing0 = 0;
                    init_config.Timing1 = 0x14;
                    break;
                case "800": //800

                    init_config.Timing0 = 0;
                    init_config.Timing1 = 0x16;
                    break;
                case "666": //666

                    init_config.Timing0 = 0x80;
                    init_config.Timing1 = 0xb6;
                    break;
                case "500": //500

                    init_config.Timing0 = 0;
                    init_config.Timing1 = 0x1c;
                    break;
                case "400"://400

                    init_config.Timing0 = 0x80;
                    init_config.Timing1 = 0xfa;
                    break;
                case "250"://250

                    init_config.Timing0 = 0x01;
                    init_config.Timing1 = 0x1c;
                    break;
                case "200"://200

                    init_config.Timing0 = 0x81;
                    init_config.Timing1 = 0xfa;
                    break;
                case "125"://125

                    init_config.Timing0 = 0x03;
                    init_config.Timing1 = 0x1c;
                    break;
                case "100"://100

                    init_config.Timing0 = 0x04;
                    init_config.Timing1 = 0x1c;
                    break;
                case "80"://80

                    init_config.Timing0 = 0x83;
                    init_config.Timing1 = 0xff;
                    break;
                case "50"://50

                    init_config.Timing0 = 0x09;
                    init_config.Timing1 = 0x1c;
                    break;
                default:
                    return "Wrong Baudrate\n";
            }

            init_config.Mode = 0;

            if (ECANDLL.OpenDevice(1, 0, 0) != ECAN.ECANStatus.STATUS_OK)
            {
                return "Open device fault!\n";
            }
            //Set can1 baud
            if (ECANDLL.InitCAN(1, 0, 0, ref init_config) != ECAN.ECANStatus.STATUS_OK)
            {
                ECANDLL.CloseDevice(1, 0);
                return "Init can fault!\n";
            }




            m_connect = 1;
            this.mCan.EnableProc = true;
            stack.IsEnabled = true;
            //IncludeTextMessage("Open Success");


            if (m_connect == 0)
            {
                return "Not open device!!\n";
            }

            //Start Can1

            if (ECANDLL.StartCAN(1, 0, 0) == ECAN.ECANStatus.STATUS_OK)
            {
                rtn_text += "Start CAN1 Success\n";
                dispatcherTimer.IsEnabled = true;
                info.IsEnabled = true;
                connect.IsEnabled = false;
                close.IsEnabled = true;
            }
            else
            {
                rtn_text += "Start Fault\n";
            }
            return rtn_text;
        }
        private string close_init()
        {
            close.IsEnabled = false;
            connect.IsEnabled = true;
            info.IsEnabled = false;
            if (m_connect == 1)
            {
                SendMessage("0", null, null);
                m_connect = 0;
                this.mCan.EnableProc = false;
                ECANDLL.CloseDevice(1, 0);

                stack.IsEnabled = false;
                return "Close Success\n";
            }
            else
                return "Close Fail\n";
        }

        private void cmd_apply(string line)
        {
            if (line != string.Empty)
            {
                string[] commands = line.Split(' ');
                try
                {
                    string option = commands[0];
                    rtnOption = option.ToUpper();
                    if (commands.Length == 3)
                    {
                        string cmdtext = parse_cmd(option, commands[1], commands[2], "5"); //DEFAULT CYCLE 5 ROUND
                        cmd.AppendText(cmdtext);
                    }
                    else if (commands.Length == 2)
                    {
                        string cmdtext = parse_cmd(option, commands[1], null, null);
                        cmd.AppendText(cmdtext);
                    }
                    else if (commands.Length == 1)
                    {
                        string cmdtext = parse_cmd(option, null, null, null);
                        cmd.AppendText(cmdtext);

                    }
                    else if (commands.Length == 4)
                    {
                        string cmdtext = parse_cmd(option, commands[1], commands[2], commands[3]);
                        cmd.AppendText(cmdtext);
                    }

                    else
                    {
                        cmd.AppendText("Wrong Input\n");
                        cmd.AppendText(">");
                        cmd.CaretPosition = cmd.CaretPosition.DocumentEnd;
                    }
                }
                catch (Exception ex)
                {
                    cmd.AppendText("Error: " + ex.Message + "\n");
                    cmd.AppendText(">");
                    cmd.CaretPosition = cmd.CaretPosition.DocumentEnd;
                }

            }

        }
        #endregion

        #region UI

        private void Timer_Tick(object sender, EventArgs e)
        {
            time.Text = DateTime.Now.ToString();
            ReadError1();
            ReadMessages();
            disp_apply();
        }
        private void IncludeTextMessage(string strMsg)
        {
            detail.Items.Add(strMsg);
            detail.SelectedIndex = detail.Items.Count - 1;
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true; // this will prevent to close
            this.m_connect = 0;
            this.mCan.EnableProc = false;

            ECANDLL.CloseDevice(1, 0);

        }
        private void connect_Click(object sender, RoutedEventArgs e)
        {
            
            if (m_connect == 1)
            {
                m_connect = 0;
                this.mCan.EnableProc = false;
                ECANDLL.CloseDevice(1, 0);

                stack.IsEnabled = false;
                return;
            }

            INIT_CONFIG init_config = new INIT_CONFIG();

            init_config.AccCode = 0;
            init_config.AccMask = 0xffffff;
            init_config.Filter = 0;

            switch (m_Baudrate)
            {
                case 0: //1000

                    init_config.Timing0 = 0;
                    init_config.Timing1 = 0x14;
                    break;
                case 1: //800

                    init_config.Timing0 = 0;
                    init_config.Timing1 = 0x16;
                    break;
                case 2: //666

                    init_config.Timing0 = 0x80;
                    init_config.Timing1 = 0xb6;
                    break;
                case 3: //500

                    init_config.Timing0 = 0;
                    init_config.Timing1 = 0x1c;
                    break;
                case 4://400

                    init_config.Timing0 = 0x80;
                    init_config.Timing1 = 0xfa;
                    break;
                case 5://250

                    init_config.Timing0 = 0x01;
                    init_config.Timing1 = 0x1c;
                    break;
                case 6://200

                    init_config.Timing0 = 0x81;
                    init_config.Timing1 = 0xfa;
                    break;
                case 7://125

                    init_config.Timing0 = 0x03;
                    init_config.Timing1 = 0x1c;
                    break;
                case 8://100

                    init_config.Timing0 = 0x04;
                    init_config.Timing1 = 0x1c;
                    break;
                case 9://80

                    init_config.Timing0 = 0x83;
                    init_config.Timing1 = 0xff;
                    break;
                case 10://50

                    init_config.Timing0 = 0x09;
                    init_config.Timing1 = 0x1c;
                    break;

            }

            init_config.Mode = 0;

            if (ECANDLL.OpenDevice(1, 0, 0) != ECAN.ECANStatus.STATUS_OK)
            {

                MessageBox.Show("Open device fault!", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }
            //Set can1 baud
            if (ECANDLL.InitCAN(1, 0, 0, ref init_config) != ECAN.ECANStatus.STATUS_OK)
            {

                MessageBox.Show("Init can fault!", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);

                ECANDLL.CloseDevice(1, 0);
                return;
            }




            m_connect = 1;
            this.mCan.EnableProc = true;
            stack.IsEnabled = true;
            IncludeTextMessage("Open Success");


            if (m_connect == 0)
            {
                MessageBox.Show("Not open device!", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);

                return;
            }

            //Start Can1

            if (ECANDLL.StartCAN(1, 0, 0) == ECAN.ECANStatus.STATUS_OK)
            {
                IncludeTextMessage("Start CAN1 Success");
                dispatcherTimer.IsEnabled = true;
                info.IsEnabled = true;
                connect.IsEnabled = false;
                close.IsEnabled = true;
            }
            else
            {
                IncludeTextMessage("Start Fault");
            }

        }
        private void cbbBaudrates_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            m_Baudrate = (byte)cbbBaudrates.SelectedIndex;

        }
        private void close_Click(object sender, RoutedEventArgs e)
        {
            string rtn_txt = close_init();
            IncludeTextMessage(rtn_txt);
        }

        private void info_Click(object sender, RoutedEventArgs e)
        {
            int i;
            BOARD_INFO mReadBoardInfo = new BOARD_INFO();

            if (ECANDLL.ReadBoardInfo(1, 0, out mReadBoardInfo) == ECANStatus.STATUS_OK)
            {
                infobox.Text = "";
                for (i = 0; i < 11; i++)

                    infobox.Text = infobox.Text + (char)(mReadBoardInfo.str_Serial_Num[i]);

            }
            else
            {

                infobox.Text = "Read info Fault";
            }
        }

        private void home_Click(object sender, RoutedEventArgs e)
        {
            running_func = 1;
            status.Text = "Status: " + funcKeyValuePairs[running_func];
            SendMessage(running_func.ToString(),distance.Text,speed.Text);
        }
        private void stop_Click(object sender, RoutedEventArgs e)
        {
            running_func = 2;
            status.Text = "Status: " + funcKeyValuePairs[running_func];
            SendMessage(running_func.ToString(), distance.Text, speed.Text);
        }
        private void d1_Click(object sender, RoutedEventArgs e)
        {
            running_func = 3;
            status.Text = "Status: " + funcKeyValuePairs[running_func];
            SendMessage("0",null,null);
            SendMessage(running_func.ToString(), distance.Text, speed.Text);

        }

        private void dc_Click(object sender, RoutedEventArgs e)
        {
            running_func = 5;
            status.Text = "Status: " + funcKeyValuePairs[running_func];
            SendMessage("0", null, null);
            SendMessage(running_func.ToString(), distance.Text, speed.Text);
        }
        private void SendMessage(string can_func,string? p1,string? p2)
        {
            
            if (this.m_connect == 0)
            {
                MessageBox.Show("Not open device!", "Error!", MessageBoxButton.OK, MessageBoxImage.Hand);
            }
            else
            {
                CAN_OBJ frameinfo;

                // We create a TPCANMsg message structure 
                //
                frameinfo = new CAN_OBJ();
                frameinfo.SendType = 0;

                frameinfo.data = new byte[8];
                frameinfo.Reserved = new byte[2];

                // We configurate the Message.  The ID (max 0x1FF),
                // Length of the Data, Message Type (Standard in 
                // this example) and die data
                //
                frameinfo.ID = Convert.ToUInt32("0", 16);
                frameinfo.DataLen = Convert.ToByte(8);
                frameinfo.ExternFlag = 0;
                frameinfo.RemoteFlag = 0;

                int tlen = frameinfo.DataLen - 1;
                
                string dis_param= "0";
                string f_param = "0";
                string b_param = "0";
                double speed_ratio;
                string speed_param = "3";
                if (p2 != String.Empty && p2 != "0" && p2!= null)
                {
                    speed_ratio = 600 * int.Parse(p2) / 0.3806;
                    speed_param = p2;
                }
                else
                    speed_ratio = 600 * 3 / 0.3806;

                if (p1 != String.Empty && p1 != "0" && p1 != null)
                {
                    dis_param = ((int)Math.Round(float.Parse(p1) * 1000/(speed_ratio/1000))).ToString().PadLeft(4, '0');
                    f_param = int.Parse(dis_param.Substring(0, 2)).ToString("X");
                    b_param = int.Parse(dis_param.Substring(2, 2)).ToString("X");
                }

                frameinfo.data[0] = Convert.ToByte(can_func, 0x10);
                frameinfo.data[1] = Convert.ToByte(f_param, 0x10);
                frameinfo.data[2] = Convert.ToByte(b_param, 0x10);
                frameinfo.data[3] = Convert.ToByte(speed_param, 0x10);
                frameinfo.data[4] = Convert.ToByte(count_round.ToString(), 0x10);

                this.mCan.gSendMsgBuf[this.mCan.gSendMsgBufHead].ID = frameinfo.ID;
                this.mCan.gSendMsgBuf[this.mCan.gSendMsgBufHead].DataLen = frameinfo.DataLen;
                this.mCan.gSendMsgBuf[this.mCan.gSendMsgBufHead].data = frameinfo.data;
                this.mCan.gSendMsgBuf[this.mCan.gSendMsgBufHead].ExternFlag = frameinfo.ExternFlag;
                this.mCan.gSendMsgBuf[this.mCan.gSendMsgBufHead].RemoteFlag = frameinfo.RemoteFlag;
                this.mCan.gSendMsgBufHead += 1;
                if (this.mCan.gSendMsgBufHead >= ComProc.SEND_MSG_BUF_MAX)
                {
                    this.mCan.gSendMsgBufHead = 0;
                }
                rtnTimeS = System.Diagnostics.Stopwatch.StartNew();
            }
        }
        
        private void ReadError1()
        {

            CAN_ERR_INFO mErrInfo = new CAN_ERR_INFO();

            if (ECANDLL.ReadErrInfo(1, 0, 0, out mErrInfo) == ECANStatus.STATUS_OK)
            {
                errinfo.Text = string.Format("{0:X4}h", mErrInfo.ErrCode);
                rxinfo.Text = string.Format("{0:X4}h", mErrInfo.Passive_ErrData[1]);
                txinfo.Text = string.Format("{0:X4}h", mErrInfo.Passive_ErrData[2]);

            }
            else
            {

                errinfo.Text = "Read Error Fault";
            }



        }
        private void ReadMessages()
        {
            string running_status = funcKeyValuePairs[running_func];
            

            CAN_OBJ frameinfo = new CAN_OBJ();
            int mCount = 0;
            
            

            while (this.mCan.gRecMsgBufHead != this.mCan.gRecMsgBufTail)
            {
                string str = "";
                frameinfo = this.mCan.gRecMsgBuf[this.mCan.gRecMsgBufTail];
                this.mCan.gRecMsgBufTail += 1;
                
                if (this.mCan.gRecMsgBufTail >= ComProc.REC_MSG_BUF_MAX)
                {
                    this.mCan.gRecMsgBufTail = 0;
                }

                // START RECEIVED WHY CANT RECEIVED???????????
                if (frameinfo.ID == 1)
                {
                    if (frameinfo.data[0].ToString() == "0")
                    {
                        count_round += 1;
                        count.Text = "Received Count: " + count_round.ToString();
                        
                    }
                }
                else if(frameinfo.ID == 0)
                {
                    rtnTimeS.Stop();
                    string elapsedMs = rtnTimeS.ElapsedMilliseconds.ToString();
                    Wait(elapsedMs);
                    if(cmdslist != null)
                    {
                        taskdone += 1;
                        isaaply = true;
                    }
                    

                }



                if (str!=string.Empty)
                    this.detail.Items.Add(str);
                if (this.detail.Items.Count > 500)
                {
                    this.detail.Items.Clear();
                }
                mCount++;
                if (mCount >= 50)
                {
                    break;
                }
                //Application.DoEvents();
            }



        }
        #endregion

        #region CMD
        private int ColumnNumber()
        {
            TextPointer caretPos = cmd.CaretPosition;
            TextPointer p = cmd.CaretPosition.GetLineStartPosition(0);
            int currentColumnNumber = Math.Max(p.GetOffsetToPosition(caretPos) - 1, 0);

            return currentColumnNumber;
        }
        private void cmd_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            int current_caret = ColumnNumber();
            if (e.Key == Key.Up)
            {
                e.Handled = true;
            }

            if (current_caret == 1)
            {
                if(e.Key == Key.Back || e.Key == Key.Left)
                    e.Handled = true;
            }       
        }

        private void cmd_KeyUp(object sender, KeyEventArgs e)
        {
            string richText = new TextRange(cmd.Document.ContentStart, cmd.Document.ContentEnd).Text.Trim();
            string currentText = richText.Split('\n').Last().Trim();

            if (e.Key == Key.Enter)
            {
                currentText = currentText[1..]; // REMOVE >
                if (currentText != string.Empty)
                {
                    string[] commands = currentText.Split(' ');
                    try
                    {
                        string option = commands[0];
                        rtnOption = option.ToUpper();
                        if (commands.Length == 3)
                        {
                            string cmdtext = parse_cmd(option, commands[1], commands[2], "5"); //DEFAULT CYCLE 5 ROUND
                            cmd.AppendText(cmdtext);
                        }
                        else if (commands.Length == 2)
                        {
                            string cmdtext = parse_cmd(option, commands[1],null, null);
                            cmd.AppendText(cmdtext);
                        }
                        else if (commands.Length == 1)
                        {
                            string cmdtext = parse_cmd(option, null, null, null);
                            cmd.AppendText(cmdtext);
                           
                        }
                        else if (commands.Length == 4)
                        {
                            string cmdtext = parse_cmd(option, commands[1], commands[2], commands[3]);
                            cmd.AppendText(cmdtext);
                        }

                        else
                        {
                            cmd.AppendText("Wrong Input\n");
                            cmd.AppendText(">");
                            cmd.CaretPosition = cmd.CaretPosition.DocumentEnd;
                        }
                    }
                    catch (Exception ex)
                    {
                        cmd.AppendText("Error: " + ex.Message + "\n");
                        cmd.AppendText(">");
                        cmd.CaretPosition = cmd.CaretPosition.DocumentEnd;
                    }
                    
                }

                // WAIT RESPONSE
                if (recording)
                {
                    FileProcess(currentText);
                }
            }
            
        }
        private void Wait(string responseTime)
        {
            cmd.AppendText(rtnOption +" Used Time: " + responseTime + " ms\n");
            cmd.AppendText(">");
            cmd.CaretPosition = cmd.CaretPosition.DocumentEnd;
        }

        private string parse_cmd(string option,string? p1, string? p2,string? p3)
        {
            string rtn_str = "";

            // common function
            switch (option)
            {
                case "--h":
                    string rtn_help = "GCAN CLI V1\n" +
                        "Usage\n" +
                        "\ti\t[1000]\t\tInit\n" +
                        "\tn\t\t\tClose\n" +
                        "\th\t\t\tHome\n" +
                        "\ts\t\t\tStop\n" +
                        "\tl\t[D]\t[S]\tLeft Move\n" +
                        "\tr\t[D]\t[S]\tRight Move\n" +
                        "\tc\t[D]\t[S]\tCycle Move\n" + 
                        "Other\n" +
                        "\t--help\t\t\tShow Option\n" +
                        "\tclear\t\t\tClear Block\n"
                        ;
                    cmd.AppendText(rtn_help);
                    cmd.AppendText(">");
                    cmd.CaretPosition = cmd.CaretPosition.DocumentEnd;
                    return rtn_str;
                case "clear":
                    cmd.Document.Blocks.Clear();
                    cmd.AppendText(">");
                    cmd.CaretPosition = cmd.CaretPosition.DocumentEnd;
                    return rtn_str;
                    
            }

            if (m_connect == 1)
            {
                switch (option)
                {
                    case "i":
                        cmd.AppendText(init(p1));
                        cmd.AppendText(">");
                        cmd.CaretPosition = cmd.CaretPosition.DocumentEnd;
                        break;

                    case "n":
                        cmd.AppendText(close_init());
                        cmd.AppendText(">");
                        cmd.CaretPosition = cmd.CaretPosition.DocumentEnd;
                        break;
                    case "h":
                        running_func = 1;
                        rtn_str = "Status: " + funcKeyValuePairs[running_func] + "\n";
                        SendMessage("0", null, null);
                        SendMessage(running_func.ToString(), p1, null);

                        break;
                    case "s":
                        running_func = 2;
                        string rtn_stop = "Status: " + funcKeyValuePairs[running_func] + "\n";
                        SendMessage(running_func.ToString(), null, null);
                        cmd.AppendText(rtn_stop);
                        cmd.AppendText(">");
                        cmd.CaretPosition = cmd.CaretPosition.DocumentEnd;
                        break;
                    case "l":
                        running_func = 3;
                        rtn_str = "Status: " + funcKeyValuePairs[running_func] + "\n";
                        SendMessage("0", null, null);
                        SendMessage(running_func.ToString(), p1, p2);
                        break;
                    case "r":
                        running_func = 4;
                        rtn_str = "Status: " + funcKeyValuePairs[running_func] + "\n";
                        SendMessage("0", null, null);
                        SendMessage(running_func.ToString(), p1, p2);
                        break;
                    case "c":
                        running_func = 5;
                        rtn_str = "Status: " + funcKeyValuePairs[running_func] + "\n";
                        // FIRST ROUND JUST HIT, SO SHOULD +1
                        count_round = int.Parse(p3)+1;
                        SendMessage("0", null, null);
                        SendMessage(running_func.ToString(), p1, p2);
                        break;
                    default:
                        break;

                }
            }
            else
                switch (option)
                {
                    case "i":
                        cmd.AppendText(init(p1));
                        cmd.AppendText(">");
                        cmd.CaretPosition = cmd.CaretPosition.DocumentEnd;
                        break;
                    
                    default:
                        cmd.AppendText("Not open device!\n"); 
                        cmd.AppendText(">");
                        cmd.CaretPosition = cmd.CaretPosition.DocumentEnd;
                        break;
                }


            return rtn_str;
        }
        #endregion

        #region Study

        
        private void learn_Click(object sender, RoutedEventArgs e)
        {
            if (recording)
            {
                recording = false;
                learn.Content = "Teach";
                learn.Background = Brushes.Lime;
            }
            else
            {
                recording = true;
                learn.Content = "Stop";
                learn.Background = Brushes.Red;
            }
                
        }

        private void FileProcess(string line)
        {
            string filename = DateTime.Now.ToString("MM-dd");
            using (StreamWriter w = File.AppendText("D:\\GIN\\Motor_UI\\Motor_V2\\cmds\\" + filename))
            {
                w.WriteLine(line);
            }
        }

        private void apply_Click(object sender, RoutedEventArgs e)
        {
            string filename = studypath.Text;
            cmdslist = BuildCmdList(filename);
            isaaply = true;
        }
        private List<string> BuildCmdList(string path)
        {
            List<string> result = new List<string>();
            foreach (var record in File.ReadLines(path))
            {
                result.Add(record);
            }
            return result;
        }
        private void disp_apply()
        {
            // study
            if (isaaply)
            {
                if(taskdone<cmdslist.Count)
                {
                    string cmdline = cmdslist[taskdone];
                    
                        
                    cmd.AppendText(cmdline+"\n");
                    cmd_apply(cmdline);
                    if (cmdline[0] == 'i' || cmdline[0] == 'n' || cmdline[0] == 's')
                    {
                        taskdone += 1;
                        isaaply = true;
                    }
                    else
                        isaaply = false;
                }
                else
                {
                    cmdslist.Clear();
                    taskdone = 0;
                    isaaply = false;
                }
            }
            
        }
        #endregion
    }

}
