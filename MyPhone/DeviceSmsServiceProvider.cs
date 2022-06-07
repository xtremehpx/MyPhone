﻿using CommunityToolkit.Mvvm.DependencyInjection;
using GoodTimeStudio.MyPhone.Services;
using MyPhone.OBEX;
using MyPhone.OBEX.Map;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;

namespace GoodTimeStudio.MyPhone
{
    /// <summary>
    /// Bluetooth Message Access Profile Manager
    /// </summary>
    public class DeviceSmsServiceProvider : BaseDeviceServiceProvider
    {
        private readonly BluetoothDevice _device;
        private readonly IMessageNotificationService _notificationService;

        private BluetoothMasClientSession? _masClientSession;
        private BluetoothMnsServerSession _mnsServerSession;

        private bool _firstStart = true;

        public DeviceSmsServiceProvider(BluetoothDevice bluetoothDevice) : base(bluetoothDevice)
        {
            _device = bluetoothDevice;
            _notificationService = Ioc.Default.GetRequiredService<IMessageNotificationService>();
            _mnsServerSession = new BluetoothMnsServerSession();
        }

        protected override async Task<bool> ConnectToServiceAsync()
        {
            try
            {
                _masClientSession = new BluetoothMasClientSession(_device);
                await _masClientSession.ConnectAsync();
                if (_firstStart)
                {
                    await _mnsServerSession.StartServerAsync();
                    _mnsServerSession.ClientAccepted += _mnsServerSession_ClientAccepted;
                    _firstStart = false;
                }

                Debug.Assert(_masClientSession.ObexClient != null);
                await _masClientSession.ObexClient.SetNotificationRegistration(true);
                return true;
            }
            catch (BluetoothDeviceNotAvailableException) 
            {
                _masClientSession!.Dispose();
                _masClientSession = null;
                return false;
            }
            catch (BluetoothObexSessionException ex)
            {
                _masClientSession!.Dispose();
                _masClientSession = null;
                throw new DeviceServiceException(ex.Message, ex);
            }
        }

        private void _mnsServerSession_ClientAccepted(BluetoothObexServerSession<MnsServer> sender, BluetoothObexServerSessionClientAcceptedEventArgs<MnsServer> e)
        {
            e.ObexServer.MessageReceived += ObexServer_MessageReceived;
        }

        private async void ObexServer_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            Debug.Assert(_masClientSession != null);
            Debug.Assert(_masClientSession.ObexClient != null);
            BMessage message = await _masClientSession.ObexClient.GetMessageAsync(e.MessageHandle);
            _notificationService.ShowMessageNotification(message);
            Debug.WriteLine(message.Body);
        }

        public override void Dispose()
        {
            _masClientSession?.Dispose();
            _mnsServerSession.Dispose();

            base.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}