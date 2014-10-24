using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace bleBump
{
	/// <summary>
	/// Logique d'interaction pour MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private Task m_Task;
		private Process m_Process;
		private bool m_Running;

		const int CTRL_C_EVENT = 0;
		const int CTRL_BREAK_EVENT = 1;

		string m_ChangeDirStr;

		public MainWindow()
		{
			InitializeComponent();
			m_Running = false;
			Closing += OnWindowClosing;
			string adtroot = Environment.GetEnvironmentVariable("ADTROOT", EnvironmentVariableTarget.Machine);
			if (adtroot == null || adtroot == "")
			{
				logText.Text = "Error : Could not find ADTROOT";
			}
			else
			{
				m_ChangeDirStr = "cd " + adtroot + "\\sdk\\platform-tools & ";
			}
		}

		public void OnWindowClosing(object sender, CancelEventArgs e)
		{
			if (m_Running == true)
			{
				Process tempProcess;

				ExecuteCommand(m_ChangeDirStr + "taskkill /im adb.exe /f /t",
							   out tempProcess, true);
				m_Process.WaitForExit();
				m_Process.Close();
				m_Task.Wait();
			}
		}

		static void ExecuteCommand(string command, out Process process, bool waitAndClose)
		{
			ProcessStartInfo processInfo;

			processInfo = new ProcessStartInfo("cmd.exe", "/c " + command);
			processInfo.CreateNoWindow = false;
			processInfo.UseShellExecute = false;
			// *** Redirect the output ***
			processInfo.RedirectStandardError = false;
			processInfo.RedirectStandardOutput = false;

			process = Process.Start(processInfo);

			if (waitAndClose)
			{
				process.WaitForExit();
				//int exitCode = process.ExitCode;
				process.Close();
			}
			// *** Read the streams ***
			//output = process.StandardOutput.ReadToEnd();
			//error =  process.StandardError.ReadToEnd();
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			if (!m_Running)
			{
				Process tempProcess;

				ExecuteCommand(m_ChangeDirStr + "adb shell cd sdcard ; mv btsnoop_hci.log btsnoop_hci_save_" +
							   DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds.ToString() +
							   ".log", out tempProcess, true);
				ExecuteCommand(m_ChangeDirStr + "adb shell am start -a android.bluetooth.adapter.action.REQUEST_ENABLE",
							   out tempProcess, true);
				Action<object> action = (object obj) =>
				{
					Environment.SetEnvironmentVariable("BLEDUMPROOTDIR", Directory.GetCurrentDirectory());
					//string filter = "\"BluetoothLeService\"," + "\"BluetoothAdapter\"," + "\"BluetoothGatt\"";
					string filter = "";

					if (filter.Length != 0)
						filter = " -s " + filter;
					ExecuteCommand(m_ChangeDirStr + "adb logcat -v time" + filter + "> %BLEDUMPROOTDIR%\\test.txt",
								   out m_Process, false);
				};

				m_Task = Task.Factory.StartNew(action, "adbLogcat");
				startButton.Content = "Stop";
				stateLabel.Text = "Running";
				m_Running = true;
			}
			else
			{
				Process tempProcess;

				ExecuteCommand(m_ChangeDirStr + "taskkill /im adb.exe /f /t",
							   out tempProcess, true);
				m_Process.WaitForExit();
				int exitCode = m_Process.ExitCode;
				m_Process.Close();
				m_Task.Wait();
				logText.Text = Directory.GetCurrentDirectory();

				ExecuteCommand(m_ChangeDirStr + "adb pull /sdcard/btsnoop_hci.log %BLEDUMPROOTDIR%",
							   out tempProcess, true);
				logText.Text += "\n" + ("ExitCode: " + exitCode.ToString()) + "\n\n";
				string logFile = "";
				try
				{
					using (StreamReader sr = new StreamReader(Directory.GetCurrentDirectory() + "\\test.txt"))
					{
						char[] delimiterChars = { ':', '.' };

						while (sr.Peek() >= 0)
						{
							string line = sr.ReadLine() + "\n";
							if (line != "\n")
							{
								if (line != "" && line[0] != '-')
								{
									line = line.Remove(0, 6);
									int spaceIdx = line.IndexOf(" ");
									string datePart = line.Substring(0, spaceIdx + 1);
									line = line.Remove(0, spaceIdx + 1);
									string[] words = datePart.Split(delimiterChars);
									DateTime time = new DateTime(1, 1, 1, int.Parse(words[0]), int.Parse(words[1]), int.Parse(words[2]), int.Parse(words[3]));
									line = time.ToString() + " " + line;
								}
								logFile += line;
							}
						}
					}
				}
				catch (Exception exception)
				{
					logText.Text += "Can't open the file : " + exception.ToString();
				}
				logText.Text += logFile;
				startButton.Content = "Start";
				stateLabel.Text = "Stopped";
				m_Running = false;
			}
		}
	}
}
