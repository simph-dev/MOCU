using InterprocessCommunication;
using DaemonsRelated;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Collections;
using System.Linq;


namespace AudioModule
{
    public class AudioHostSideBridge : AudioDaemon_API, IDaemonHostBridge
    {
        private IInterprocessCommunicator _communicator;

        public event Action<IEnumerable<AudioDeviceData>> DevicesDataChanged;
        public event Action<IEnumerable<AudioClipData>> ClipsDataChanged;
        public event Action<IEnumerable<AudioIntercomData>> IntercomStatesChanged;
        public event Action<IEnumerable<DaemonErrorReport>> ErrorsOccurred;

        public AudioHostSideBridge(IInterprocessCommunicator communicator)
        {
            _communicator = communicator;
            _communicator.MessageReceived += message => HandleIncomingMessage(message);
        }
        

        public void StartCommunication()
        {
            // method is async, but there is no need to use 'await'
            _communicator.Start();
        }

        public void TestMethod1()
        {
            /*try
            {
                *//*var testObj = new AudioDataTransferObject
                {
                    PlayClipCommands = new List<PlayAudioClipCommand>() {
                        new PlayAudioClipCommand() {
                            InterruptPlayingClips = true,
                            OutputDeviceId = Guid.Parse("515a3dc0-ca38-4fb9-aa8b-6d268cbd94ac"),
                            ClipData = new AudioClipData() {
                                name = AudioClipName.PingDevice
                            }
                        }
                    },

                    DeviceChanges = new List<AudioDeviceData>()
                    {
                        new AudioDeviceData()
                        {
                            Id = Guid.Parse("515a3dc0-ca38-4fb9-aa8b-6d268cbd94ac"),
                            Volume = 40
                        }
                    }
                };*/

            /*for (int i = 0; i < 10; i++)
            {
                var testObj = new AudioDataTransferObject
                {
                    PlayClipCommands = new List<PlayAudioClipCommand>() {
                        new PlayAudioClipCommand() {
                            InterruptPlayingClips = true,
                            OutputDeviceId = Guid.Parse("515a3dc0-ca38-4fb9-aa8b-6d268cbd94ac"),
                            ClipData = new AudioClipData() {
                                name = AudioClipName.PingDevice
                            }
                        }
                    }
                };
                _communicator.SendMessage(JsonHelper.SerializeJson(testObj));
            }*//*

        }
        catch { }*/


            try
            {
                var testObj = new AudioDataTransferObject
                {
                    PlayClipCommands = new List<PlayAudioClipCommand> {
                        new PlayAudioClipCommand {
                            InterruptPlayingClips = true,
                            OutputDeviceId = Guid.Parse("515a3dc0-ca38-4fb9-aa8b-6d268cbd94ac"),
                            ClipData = new AudioClipData {
                                name = AudioClipName.PingDevice
                            }
                        },

                        /*new PlayAudioClipCommand() {
                            InterruptPlayingClips = false,
                            OutputDeviceId = Guid.Parse("f6e626d3-fc5c-44ab-a993-81e2cbaccdeb"),
                            ClipData = new AudioClipData() {
                                name = AudioClipName.CorrectAnswer
                            }
                        }*/
                    },

                    IntercomCommands = new List<AudioIntercomData>() {
                        new AudioIntercomData {
                            isOn = true,
                            id = new Guid("ed3c3975-74c7-433a-b9ae-96cfd0e8a000"),
                            fromDevices = new List<Guid> { Guid.Parse("702dbf2f-aae4-48d1-9c6e-a227bab8d4ae") },
                            toDevices = new List<Guid> { Guid.Parse("8892b770-5b71-49d5-b8a9-6194ee8ddfbc") },
                        }
                    }

                    /*DeviceChanges = new List<AudioDeviceData>()
                    {
                        new AudioDeviceData()
                        {
                            Id = Guid.Parse("515a3dc0-ca38-4fb9-aa8b-6d268cbd94ac"),
                            Volume = 30
                        }
                    }*/
                };

                /*var testObj = new AudioDataTransferObject
                {
                    IntercomCommands = new List<AudioIntercomData>() {
                        new AudioIntercomData() {
                            isOn = true,
                            id = new Guid("ed3c3975-74c7-433a-b9ae-96cfd0e8a000"),
                            fromDevices = new List<Guid> { Guid.Parse("ed3c3975-74c7-433a-b9ae-96cfd0e8a426"), Guid.Parse("41389b3f-51ea-4dce-b644-47338a8aadb5") },
                            toDevices = new List<Guid> { Guid.Parse("f6e626d3-fc5c-44ab-a993-81e2cbaccdeb"), Guid.Parse("515a3dc0-ca38-4fb9-aa8b-6d268cbd94ac") },
                        }
                    }
                };*/

                _communicator.SendMessage(JsonHelper.SerializeJson(testObj));
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Error in 'AudioHostSideBridge.TestMethod1': {ex}");
            }
        }

        public void TestMethod2()
        {
            /*try
            {
                var testObj = new AudioDataTransferObject
                {
                    PlayClipCommands = new List<PlayAudioClipCommand>() {
                        new PlayAudioClipCommand() {
                            InterruptPlayingClips = true,
                            OutputDeviceId = Guid.Parse("515a3dc0-ca38-4fb9-aa8b-6d268cbd94ac"),
                            ClipData = new AudioClipData() {
                                name = AudioClipName.PingDevice
                            }
                        }
                    },

                    DeviceChanges = new List<AudioDeviceData>()
                    {
                        new AudioDeviceData()
                        {
                            Id = Guid.Parse("515a3dc0-ca38-4fb9-aa8b-6d268cbd94ac"),
                            Volume = 60
                        }
                    }
                };

                _communicator.SendMessage(JsonHelper.SerializeJson(testObj));
            }
            catch { }*/

            try
            {
                var testObj = new AudioDataTransferObject
                {
                    IntercomCommands = new List<AudioIntercomData> {
                        new AudioIntercomData {
                            isOn = false,
                            id = new Guid("ed3c3975-74c7-433a-b9ae-96cfd0e8a000")
                        }
                    }
                };

                _communicator.SendMessage(JsonHelper.SerializeJson(testObj));
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Error in 'AudioHostSideBridge.TestMethod1': {ex}");
            }
        }

        // ########################################################################################

        public void PlayClips(IEnumerable<PlayAudioClipCommand> playCommandsData)
        {
            var audioDataTransferObject = new AudioDataTransferObject { PlayClipCommands = playCommandsData };
            var json = JsonHelper.SerializeJson(audioDataTransferObject);
            _communicator.SendMessage(json);
        }

        public void UpdateClipsData(IEnumerable<AudioClipData> clipsData)
        {
            var audioDataTransferObject = new AudioDataTransferObject { ClipChanges = clipsData };
            var json = JsonHelper.SerializeJson(audioDataTransferObject);
            _communicator.SendMessage(json);
        }

        public void UpdateDevicesData(IEnumerable<AudioDeviceData> devicesData)
        {
            var audioDataTransferObject = new AudioDataTransferObject { DeviceChanges = devicesData };
            var json = JsonHelper.SerializeJson(audioDataTransferObject);
            _communicator.SendMessage(json);
        }

        public void UpdateIntercomStates(IEnumerable<AudioIntercomData> intercomsData)
        {
            var audioDataTransferObject = new AudioDataTransferObject { IntercomCommands = intercomsData };
            var json = JsonHelper.SerializeJson(audioDataTransferObject);
            _communicator.SendMessage(json);
        }

        // ########################################################################################

        private void HandleIncomingMessage(string message)
        {
            try
            {
                var dataTransferObject = JsonHelper.DeserializeJson<AudioDataTransferObject>(message);

                // CUSTOM MESSAGE
                if (!String.IsNullOrEmpty(dataTransferObject.CustomMessage))
                    UnityEngine.Debug.Log($"Custom message in 'HandleIncomingMessage': {dataTransferObject.CustomMessage}");

                // CLIP CHANGES
                if (dataTransferObject.ClipChanges.Any())
                    ClipsDataChanged?.Invoke(dataTransferObject.ClipChanges);

                // DEVICE CHANGES
                if (dataTransferObject.DeviceChanges.Any())
                    DevicesDataChanged?.Invoke(dataTransferObject.DeviceChanges);

                // INTERCOM COMMANDS
                if (dataTransferObject.IntercomCommands.Any())
                    IntercomStatesChanged?.Invoke(dataTransferObject.IntercomCommands);

                /*// PLAY CLIP COMMANDS
                if (dataTransferObject.PlayClipCommands.Any())
                    PlayClips?.Invoke(dataTransferObject.PlayClipCommands);*/

                /*// TERMINATION COMMAND
                if (dataTransferObject.DoTerminateTheDaemon)
                    TerminateDaemon?.Invoke("Got command from host to terminate the daemon");*/
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Error in 'HandleIncomingMessage': {ex.Message}");
            }
        }
    }
}