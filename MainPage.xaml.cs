using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Devices.Enumeration;
using Windows.Devices.Midi;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI.Input.Preview.Injection;

namespace MidiPageTurner
{
    public sealed partial class MainPage : Page
    {
        private readonly MidiDeviceWatcher _inputDeviceWatcher;
        private MidiInPort _midiInPort;
        private readonly InputInjector _inputInjector;

        private readonly byte _triggerThreshold = 20;
        private readonly byte[] _midiTriggerOptions = { 67, 66 };
        private readonly VirtualKey[] _pageTurnKeyOptions = { VirtualKey.Right, VirtualKey.PageDown, VirtualKey.Space};
        private byte _currentMidiTrigger;
        private VirtualKey _currentPageTurnKey;

        private DateTime _lastTriggerTime = DateTime.Now;
        private readonly TimeSpan _cooldown = TimeSpan.FromMilliseconds(750);

        public MainPage()
        {
            InitializeComponent();
            _inputInjector = InputInjector.TryCreate();
            _ = EnumerateMidiInputDevices();
            _inputDeviceWatcher = new MidiDeviceWatcher(MidiInPort.GetDeviceSelector(), MidiInListBox, Dispatcher);

            _inputDeviceWatcher.StartWatcher();
        }

        private void MidiInPort_MessageReceived(MidiInPort sender, MidiMessageReceivedEventArgs args)
        {
            var receivedMidiMessage = args.Message;

            if (DateTime.Now - _lastTriggerTime < _cooldown) return;
            if (receivedMidiMessage is MidiControlChangeMessage controlChangeMessage)
            {
                if (controlChangeMessage.Controller == _currentMidiTrigger && controlChangeMessage.ControlValue >= _triggerThreshold)
                {
                    _lastTriggerTime = DateTime.Now;
                    var info = new InjectedInputKeyboardInfo
                    {
                        VirtualKey = (ushort)_currentPageTurnKey
                    };
                    _inputInjector.InjectKeyboardInput(new[] { info });
                }
            }
        }

        private async Task EnumerateMidiInputDevices()
        {
            // Find all input MIDI devices
            var midiInputQueryString = MidiInPort.GetDeviceSelector();
            var midiInputDevices = await DeviceInformation.FindAllAsync(midiInputQueryString);

            MidiInListBox.Items.Clear();

            // Return if no external devices are connected
            if (midiInputDevices.Count == 0)
            {
                MidiInListBox.Items.Add("No MIDI input devices found!");
                MidiInListBox.IsEnabled = false;
                return;
            }

            // Else, add each connected input device to the list
            foreach (var deviceInfo in midiInputDevices)
            {
                MidiInListBox.Items.Add(deviceInfo.Name);
            }
            MidiInListBox.IsEnabled = true;
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            _currentMidiTrigger = _midiTriggerOptions[MidiTriggerListBox.SelectedIndex];
            _currentPageTurnKey = _pageTurnKeyOptions[PageTurnKeyListBox.SelectedIndex];

            var deviceInformationCollection = _inputDeviceWatcher.DeviceInformationCollection;
            var devInfo = deviceInformationCollection?[MidiInListBox.SelectedIndex];
            if (devInfo == null)
            {
                return;
            }
            _midiInPort = await MidiInPort.FromIdAsync(devInfo.Id);

            if (_midiInPort == null)
            {
                System.Diagnostics.Debug.WriteLine("Unable to create MidiInPort from input device");
                return;
            }
            _midiInPort.MessageReceived += MidiInPort_MessageReceived;
        }

        private async void RefreshInputButton_Click(object sender, RoutedEventArgs e)
        {
            await EnumerateMidiInputDevices();
        }
    }
}
