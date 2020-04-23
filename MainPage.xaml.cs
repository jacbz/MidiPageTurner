using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Devices.Enumeration;
using Windows.Devices.Midi;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI.Input.Preview.Injection;

namespace MidiPageTurner
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        MyMidiDeviceWatcher inputDeviceWatcher;
        MidiInPort midiInPort;
        List<IMidiMessage> messages = new List<IMidiMessage>();
        InputInjector inputInjector;

        private readonly byte TRIGGER_TRESHOLD = 20;
        private readonly byte SOFT_PEDAL_CONTROLLER = 67;
        private readonly byte SOSTENUTO_PEDAL_CONTROLLER = 66;

        private DateTime lastTriggerTime = DateTime.Now;
        private TimeSpan cooldown = TimeSpan.FromMilliseconds(750);

        public MainPage()
        {
            this.InitializeComponent();
            inputInjector = InputInjector.TryCreate();
            EnumerateMidiInputDevices();
            inputDeviceWatcher =
                new MyMidiDeviceWatcher(MidiInPort.GetDeviceSelector(), midiInPortListBox, Dispatcher);

            inputDeviceWatcher.StartWatcher();

        }

        private async void midiInPortListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var deviceInformationCollection = inputDeviceWatcher.DeviceInformationCollection;

            if (deviceInformationCollection == null)
            {
                return;
            }

            DeviceInformation devInfo = deviceInformationCollection[midiInPortListBox.SelectedIndex];

            if (devInfo == null)
            {
                return;
            }

            midiInPort = await MidiInPort.FromIdAsync(devInfo.Id);

            if (midiInPort == null)
            {
                System.Diagnostics.Debug.WriteLine("Unable to create MidiInPort from input device");
                return;
            }
            midiInPort.MessageReceived += MidiInPort_MessageReceived;
        }

        private void MidiInPort_MessageReceived(MidiInPort sender, MidiMessageReceivedEventArgs args)
        {
            IMidiMessage receivedMidiMessage = args.Message;
            messages.Add(receivedMidiMessage);
            System.Diagnostics.Debug.WriteLine(receivedMidiMessage.Timestamp.ToString());

            if (DateTime.Now - lastTriggerTime < cooldown) return;

            if (receivedMidiMessage.Type == MidiMessageType.ControlChange)
            {
                var controlChangeMessage = (MidiControlChangeMessage)receivedMidiMessage;
                if (controlChangeMessage.Controller == SOFT_PEDAL_CONTROLLER && controlChangeMessage.ControlValue >= TRIGGER_TRESHOLD)
                {
                    lastTriggerTime = DateTime.Now;

                    var info = new InjectedInputKeyboardInfo();
                    info.VirtualKey = (ushort)(VirtualKey.Right);
                    inputInjector.InjectKeyboardInput(new[] { info });
                }
            }
        }

        private async Task EnumerateMidiInputDevices()
        {
            // Find all input MIDI devices
            string midiInputQueryString = MidiInPort.GetDeviceSelector();
            DeviceInformationCollection midiInputDevices = await DeviceInformation.FindAllAsync(midiInputQueryString);

            midiInPortListBox.Items.Clear();

            // Return if no external devices are connected
            if (midiInputDevices.Count == 0)
            {
                this.midiInPortListBox.Items.Add("No MIDI input devices found!");
                this.midiInPortListBox.IsEnabled = false;
                return;
            }

            // Else, add each connected input device to the list
            foreach (DeviceInformation deviceInfo in midiInputDevices)
            {
                this.midiInPortListBox.Items.Add(deviceInfo.Name);
            }
            this.midiInPortListBox.IsEnabled = true;
        }
    }
}
