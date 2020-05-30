using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Devices.Enumeration;
using Windows.Devices.Midi;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.System;
using Windows.UI;
using Windows.UI.Input.Preview.Injection;
using Windows.UI.Xaml.Media;

namespace MidiPageTurner
{
    public sealed partial class MainPage : Page
    {
        private readonly MidiDeviceWatcher _inputDeviceWatcher;
        private MidiInPort _midiInPort;
        private readonly InputInjector _inputInjector;

        private readonly byte _triggerThreshold = 20;
        private readonly byte[] _midiTriggerOptions = { 67, 66, 64 };

        private readonly VirtualKey[][] _pageTurnKeyOptions1 =
            {new[] {VirtualKey.Space}, new[] {VirtualKey.Right}, new[] {VirtualKey.PageDown}};

        private readonly VirtualKey[][] _pageTurnKeyOptions2 =
            {new[] {VirtualKey.Shift, VirtualKey.Space}, new[] {VirtualKey.Left}, new[] {VirtualKey.PageUp}};
        private byte _currentMidiTrigger1;
        private byte _currentMidiTrigger2;
        private VirtualKey[] _currentPageTurnKey1;
        private VirtualKey[] _currentPageTurnKey2;

        private bool _isActive = false;

        private SolidColorBrush IndicatorColor => _isActive
            ? new SolidColorBrush(Color.FromArgb(255, 39, 174, 96))
            : new SolidColorBrush(Color.FromArgb(255, 189, 195, 199));
        private DateTime _lastTriggerTime = DateTime.Now;
        private readonly TimeSpan _cooldown = TimeSpan.FromMilliseconds(750);

        public MainPage()
        {
            InitializeComponent();
            _inputInjector = InputInjector.TryCreate();
            _inputDeviceWatcher = new MidiDeviceWatcher(MidiInPort.GetDeviceSelector(), MidiInListBox, Dispatcher);
            _inputDeviceWatcher.UpdateDevices();
            _inputDeviceWatcher.StartWatcher();
        }

        private void MidiInPort_MessageReceived(MidiInPort sender, MidiMessageReceivedEventArgs args)
        {
            var receivedMidiMessage = args.Message;
            if (DateTime.Now - _lastTriggerTime < _cooldown ||
                !(receivedMidiMessage is MidiControlChangeMessage controlChangeMessage) ||
                controlChangeMessage.ControlValue < _triggerThreshold ||
                (controlChangeMessage.Controller != _currentMidiTrigger1 &&
                controlChangeMessage.Controller != _currentMidiTrigger2)) return;

            var pageTurnKeys = controlChangeMessage.Controller == _currentMidiTrigger1
                ? _currentPageTurnKey1
                : _currentPageTurnKey2;

            _lastTriggerTime = DateTime.Now;

            foreach (var key in pageTurnKeys)
            {
                _inputInjector.InjectKeyboardInput(new[] { new InjectedInputKeyboardInfo
                {
                    VirtualKey = (ushort)key
                }});
            }

            // release keys again
            foreach (var key in pageTurnKeys.Reverse())
            {
                _inputInjector.InjectKeyboardInput(new[] { new InjectedInputKeyboardInfo
                {
                    VirtualKey = (ushort)key,
                    KeyOptions = InjectedInputKeyOptions.KeyUp
                }});
            }
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            _currentMidiTrigger1 = _midiTriggerOptions[MidiTriggerListBox1.SelectedIndex];
            _currentMidiTrigger2 = _midiTriggerOptions[MidiTriggerListBox2.SelectedIndex];
            _currentPageTurnKey1 = _pageTurnKeyOptions1[PageTurnKeyListBox1.SelectedIndex];
            _currentPageTurnKey2 = _pageTurnKeyOptions2[PageTurnKeyListBox2.SelectedIndex];

            var deviceInformationCollection = _inputDeviceWatcher.DeviceInformationCollection;
            var devInfo = deviceInformationCollection?[MidiInListBox.SelectedIndex];
            if (devInfo == null)
            {
                return;
            }
            _midiInPort = await MidiInPort.FromIdAsync(devInfo.Id);

            if (_midiInPort == null)
            {
                Debug.WriteLine("Unable to create MidiInPort from input device");
                return;
            }

            _isActive = true;
            Bindings.Update();
            _midiInPort.MessageReceived += MidiInPort_MessageReceived;
        }

        private void RefreshInputButton_Click(object sender, RoutedEventArgs e)
        {
            _inputDeviceWatcher.UpdateDevices();
        }

        private void MidiInListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            StartButton.IsEnabled = MidiInListBox.SelectedIndex >= 0;
        }

        private void MidiTriggerListBox1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MidiTriggerListBox2 != null && MidiTriggerListBox2.SelectedIndex == MidiTriggerListBox1.SelectedIndex)
            {
                MidiTriggerListBox2.SelectedIndex =
                    (MidiTriggerListBox2.SelectedIndex + 1) % MidiTriggerListBox2.Items.Count;
            }
        }

        private void MidiTriggerListBox2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MidiTriggerListBox1.SelectedIndex == MidiTriggerListBox2.SelectedIndex)
            {
                MidiTriggerListBox1.SelectedIndex =
                    (MidiTriggerListBox1.SelectedIndex + 1) % MidiTriggerListBox1.Items.Count;
            }
        }
    }
}
