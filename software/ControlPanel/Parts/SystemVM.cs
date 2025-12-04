using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using ControlPanel.Models;
using ControlPanel.ViewModels;
using ControlPanelLib;
using ControlPanelLib.Models;

namespace ControlPanel.Parts {
    public class SystemVM : BaseDeviceVM {
        //private SystemModel sys = new SystemModel();

        //public RelayCommand AddCmd { get; set; }

        //public ObservableCollection<PidLoopViewModel> Pids { get; } = new ObservableCollection<PidLoopViewModel>();

        //public SystemModel Sys => sys;

        //public SystemVM() {
        //    Name = "System Settings";
        //    AddCmd = new RelayCommand(Add);

        //    sys.PidListChanged += Sys_PidListChanged;
        //}

        //private void Add() {
        //    PidLoopModel pidLoopModel = new PidLoopModel();
        //    PidLoopViewModel newVm = new PidLoopViewModel(pidLoopModel, RemovePidLoop) {
        //        Title = $"PID {Pids.Count + 1}"
        //    };
        //    Pids.Add(newVm);
        //    sys.AddPID(pidLoopModel);
        //}
        //private void RemovePidLoop(PidLoopViewModel vm) {
        //    if (Pids.Contains(vm)) {
        //        Pids.Remove(vm);
        //        sys.RemovePID(vm.Model);
        //    }
        //}

        //private void Sys_PidListChanged(object sender, EventArgs e) {
        //    Application.Current.Dispatcher.Invoke(() => {
        //        Pids.Clear();
        //        List<PidLoopModel> pidModels = sys.GetPidLoops();
        //        for (int i = 0; i < pidModels.Count; i++) {
        //            PidLoopViewModel vm = new PidLoopViewModel(pidModels[i], RemovePidLoop);
        //            Pids.Add(vm);
        //        }
        //    });
        //}

        //public override void Load() {
        //    sys.Load(); // This will trigger PidListChanged and update the Pids collection
        //}

        //public override void Save() {
        //    sys.Save();
        //}

        //public override void Stop() {
        //    sys.Server.Stop();
        //}

        //public override void Closing() {
        //    Stop();
        //}

        //public void StartModbusServer(int port = 1502) {
        //    sys.Server.Start();
        //}
    }
}
