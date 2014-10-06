vt100 Terminal Helper
=====================

The VT100 Terminal Helper (as the name suggests) helps you use your old vt100 terminal do do work on modern computers.
The helper allows you to to write and compile your own C# Plugins from scratch.

Included in this project are two Plugins:

- **DirList**; provides basic functionality to display files and load other plugins. This plugin is included in the source code and does not needs to be compiled. It starts automatically as soon as a terminal is detected
- **Net**; This plugin is provided as a TXT file for you to edit and see how plugins are made

Features
--------
- Providing access to newer hardware for old Terminals
- Allowing users to add features without the need to recompile the source
- Providing a simple way for users to talk to a vt100 terminal.

How to create a plugin
----------------------
Create a new Text file with this skeleton:

	using System;
	using System.IO;
	using VT100;
	using System.Threading;

	namespace VT100.PluginSystem
	{
		public class YOUR_PLUGIN_NAME_HERE : IPlugin
		{
			private Thread T;
			private VTconsole C;
			private bool cont;

			public bool IsRunning
			{
				get
				{
					if (T != null)
					{
						return T.IsAlive;
					}
					return false;
				}
			}

			public YOUR_PLUGIN_NAME_HERE()
			{
				//this is called when your plugin is loaded
			}
			
			public bool Start(VTconsole Con)
			{
				//this is called, if the plugin is invoked.
				if (T == null)
				{
					C = Con;

					T = new Thread(run);
					T.IsBackground = true;
					T.Start();
				}
				else
				{
					return false;
				}
				return true;
			}

			public void Stop()
			{
				if (cont && T != null)
				{
					cont = false;
					if (!T.Join(2000))
					{
						try
						{
							T.Abort();
						}
						catch
						{
						}
					}
					T = null;
				}
			}

			public void RecMessage(IPlugin source, string Message)
			{
                if (Message == "GETCOMMAND")
                {
	                //change XXX to your command. No spaces allowed
                    source.RecMessage(this, "GETCOMMAND:XXX");
                }
                else if (Message == "GETHELP")
                {
	                //change XXX to a single line of help text
                    source.RecMessage(this, "GETHELP:XXX");
                }
                else
                {
                    //Your other stuff here
                }
			}

			public void RecMessage(IPlugin source, byte[] Message, int index, int Length)
			{
			}

			private void run()
			{
				cont = true;
				while (cont)
				{
					//do your stuff here
					//set cont to "false" to exit the plugin
				}
				//do eventual cleanup here
				T = null;
			}

			public void Block()
			{
				T.Join();
			}

			public bool Block(int Timeout)
			{
				return T.Join(Timeout);
			}

			public override string ToString()
			{
				return "Your-Plugin-Name-Here";
			}
		}
	}

This plugin provides a base for asynchronous processing. You can still change all function content if you wish.
Use the RecMessage functions to receive messages and respond to them.
The DirList plugin is one of the plugins to contact you for a command and a one line help text.

You are free to add your own functions to your plugins. You can have a plugin ``A`` and a plugin ``B``, where ``A`` has an additional function ``foo()``.
Send a message from ``A`` to ``B``, ``B`` can check if the sender is of type ``A``, and then can call ``A.foo()``.

Load a plugin from your plugin
------------------------------
To load a plugin, use the following code (includes full error handler):

	try
	{
		IPlugin P = PluginManager.LoadPlugin(FILENAME);
		if (P != null)
		{
			//PLUGIN LOADED
		}
		else
		{
			//PLUGIN NOT LOADED. NOT A PLUGIN AT ALL
		}
	}
	catch(Exception ex)
	{
		if (PluginManager.LastErrors != null)
		{
			//SOURCE IS A PLUGIN, BUT INVALID SOURCE CODE
			foreach (CompilerError EE in PluginManager.LastErrors)
			{
				//Outputs all errors to the console
				C.WriteLine("[{0};{1}] {2} {3}", EE.Line, EE.Column, EE.ErrorNumber, EE.ErrorText);
			}
		}
		else
		{
			//OTHER ERROR
		}
	}

If you are absolutely sure, your plugin is valid, the line ``IPlugin P = PluginManager.LoadPlugin(FILENAME);`` is enough.

Note
----
In the source there is a file ``clsNetworkPlugin.cs``. It is not part of the source (build action in properties is "None") but is basically the network plugin.