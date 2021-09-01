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
using Windows.Data.Xml.Dom;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Input.Preview.Injection;
using Windows.UI.Notifications;
using Windows.UI.Xaml.Media;

namespace MidiPageTurner
{
    public sealed partial class MainPage : Page
    {
        private string _deviceSelectorString;
        public DeviceInformationCollection DeviceInformationCollection { get; set; }
        private MidiInPort _midiInPort;
        private string _currentDevice;

        private readonly InputInjector _inputInjector;
        private readonly byte _triggerThreshold = 20;
        private readonly byte[] _midiTriggerOptions = { 67, 66, 64 };
        private readonly VirtualKey[][] _pageTurnKeyOptions1 =
            {new[] {VirtualKey.Right}, new[] {VirtualKey.PageDown}, new[] {VirtualKey.Space}, };

        private readonly VirtualKey[][] _pageTurnKeyOptions2 =
            {new[] {VirtualKey.Left}, new[] {VirtualKey.PageUp}, new[] {VirtualKey.Shift, VirtualKey.Space}};
        private byte _currentMidiTrigger1;
        private byte _currentMidiTrigger2;
        private VirtualKey[] _currentPageTurnKey1;
        private VirtualKey[] _currentPageTurnKey2;

        private SolidColorBrush _activeBrush = new SolidColorBrush(Color.FromArgb(255, 39, 174, 96));
        private SolidColorBrush _inactiveBrush = new SolidColorBrush(Color.FromArgb(255, 189, 195, 199));
        private DateTime _lastTriggerTime = DateTime.Now;
        private readonly TimeSpan _cooldown = TimeSpan.FromMilliseconds(750);

        public MainPage()
        {
            InitializeComponent();
            InitDeviceWatcher();
            _inputInjector = InputInjector.TryCreate();
            SetBadge("unavailable");
        }

        public void InitDeviceWatcher()
        {
            _deviceSelectorString = MidiInPort.GetDeviceSelector();

            var deviceWatcher = DeviceInformation.CreateWatcher(_deviceSelectorString);
            deviceWatcher.Added += async (sender, args) =>
            {
                Log("Device added");
                await Dispatcher.RunAsync(CoreDispatcherPriority.High, UpdateDevices);
            };

            deviceWatcher.Removed += async (sender, args) =>
            {
                Log("Device removed");
                if (args.Id == _currentDevice)
                {
                    Stop();
                }
                await Dispatcher.RunAsync(CoreDispatcherPriority.High, UpdateDevices);
            };

            deviceWatcher.Updated += async (sender, args) =>
            {
                Log("Device updated");
                await Dispatcher.RunAsync(CoreDispatcherPriority.High, UpdateDevices);
            };

            deviceWatcher.EnumerationCompleted += async (sender, args) =>
            {
                Log("Device enumeration completed");
                await Dispatcher.RunAsync(CoreDispatcherPriority.High, UpdateDevices);
            };

            Log("Starting DeviceWatcher...");
            deviceWatcher.Start();
        }

        public async void UpdateDevices()
        {
            Log("Updating devices...");
            DeviceInformationCollection = await DeviceInformation.FindAllAsync(_deviceSelectorString);

            MidiInListBox.Items.Clear();

            if (!DeviceInformationCollection.Any())
            {
                Log("No MIDI input devices found");
                MidiInListBox.Items.Add("No MIDI input devices found!");
                MidiInListBox.IsEnabled = false;
                return;
            }

            foreach (var deviceInfo in DeviceInformationCollection)
            {
                Log($"Discovered MIDI Input {deviceInfo.Name}");
                MidiInListBox.Items.Add(deviceInfo.Name);
            }
            MidiInListBox.IsEnabled = true;
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
                    VirtualKey = (ushort)key,
                    KeyOptions = InjectedInputKeyOptions.ExtendedKey
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
            if (!string.IsNullOrEmpty(_currentDevice))
            {
                Stop();
            }

            Log("Obtaining MIDI information on selected device");
            _currentMidiTrigger1 = _midiTriggerOptions[MidiTriggerListBox1.SelectedIndex];
            _currentMidiTrigger2 = _midiTriggerOptions[MidiTriggerListBox2.SelectedIndex];
            _currentPageTurnKey1 = _pageTurnKeyOptions1[PageTurnKeyListBox1.SelectedIndex];
            _currentPageTurnKey2 = _pageTurnKeyOptions2[PageTurnKeyListBox2.SelectedIndex];

            var deviceInformationCollection = DeviceInformationCollection;
            var devInfo = deviceInformationCollection?[MidiInListBox.SelectedIndex];
            if (devInfo == null)
            {
                Log("Error: DeviceInformationCollection was null");
                return;
            }
            _midiInPort = await MidiInPort.FromIdAsync(devInfo.Id);

            if (_midiInPort == null)
            {
                Log("Error: Unable to create MidiInPort from input device");
                return;
            }

            await Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                _midiInPort.MessageReceived += MidiInPort_MessageReceived;
            });

            Start(devInfo.Id);
        }

        private async void Start(string id)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Log("Subscribing to selected MIDI device");
                _currentDevice = id;
                Indicator.Fill = _activeBrush;
                SetBadge("available");
            });
        }

        private async void Stop()
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Log("Unsubscribing from current MIDI device");
                _currentDevice = null;
                Indicator.Fill = _inactiveBrush;
                SetBadge("unavailable");
            });
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

        private async void Log(string text)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var time = $"({DateTime.Now:HH:mm:ss.ff})\t";
                LogTextBox.Text = string.IsNullOrEmpty(LogTextBox.Text) ? time + text : LogTextBox.Text + "\n" + time + text;
                LogScrollViewer.ChangeView(0.0f, double.MaxValue, 1.0f, true);
            });
        }

        private void SetBadge(string badgeGlyphValue)
        {
            var badgeXml = BadgeUpdateManager.GetTemplateContent(BadgeTemplateType.BadgeGlyph);
            var badgeElement = badgeXml.SelectSingleNode("/badge") as XmlElement;
            badgeElement.SetAttribute("value", badgeGlyphValue);
            var badge = new BadgeNotification(badgeXml);
            BadgeUpdateManager.CreateBadgeUpdaterForApplication().Update(badge);
        }
    }
}
