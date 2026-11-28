# HostModule

Hint: `[host]`  
Type: `PLang.Modules.HostModule.Program`

Look at what runs on the machine, as a sysadmin does: which ports listen and which program owns them, which processes run and as whom, tcp connections and counters (get tcp stats), cpu, memory and io pressure (get pressure, psi), cpu time counters (get cpu ticks), and one process's memory, threads, open files and age by pid (get process stats). Read only. Linux

## Methods

### GetCpuTicks

```
GetCpuTicks() : PLang.Modules.HostModule.CpuTicks
```

CPU time since boot in ticks (100 per second), all CPUs together, from /proc/stat. One object: Total, User (includes nice), System (includes irq and softirq), Idle, Iowait, Steal; User + System + Idle + Iowait + Steal = Total. The ticks only go up: take the difference between two reads, busy percent = 100 * (Δtotal - Δidle - Δiowait) / Δtotal. e.g. `get cpu ticks, write to %cpu%`


Examples:

- get cpu ticks, write to %cpu% =>
- get cpu time, write to %cpu% =>
- read the cpu time counters, write to %ticks% =>

### GetListeningPorts

```
GetListeningPorts() : List<ListeningPort>
```

The ports this machine listens on (tcp and udp), with the program that owns each when it can be seen (root sees all, another user only its own). Each row: Protocol, Address, Port, Process, Pid. e.g. `get listening ports, write to %ports%`


Examples:

- get listening ports, write to %ports% =>
- which ports does this machine listen on, write to %open_ports% =>

### GetPressure

```
GetPressure() : List<PressureRow>
```

How much CPU, memory and io held tasks back (pressure stall information, /proc/pressure). Each row: Resource (cpu, memory, io), Kind (some: at least one task waited, full: all non-idle tasks waited at once), Avg10, Avg60, Avg300 (percent of the last 10, 60, 300 seconds), Total (microseconds waited since boot, only goes up). cpu has no full row on older kernels; a kernel without pressure information gives an empty list. e.g. `get pressure, write to %pressure%`


Examples:

- get pressure, write to %pressure% =>
- get psi, write to %psi% =>
- get cpu pressure, write to %cpuPressure% =>
- how much is the machine waiting for cpu, memory and io, write to %psi% =>

### GetProcesses

```
GetProcesses() : List<ProcessInfo>
```

The running processes: User, Pid, Name, CpuSeconds (CPU time used so far). e.g. `get processes, write to %processes%`


Examples:

- get processes, write to %processes% =>
- list the running processes, write to %procs% =>

### GetProcessStats

```
GetProcessStats(Int32 pid) : PLang.Modules.HostModule.ProcessStats
```

One process by its pid. One object: Pid, Name, RssMb (memory it holds), Threads, Fds (open files; null when they cannot be counted, which needs root for another user's process), CpuSeconds (CPU time used so far), AgeSeconds (seconds since it started). A pid that does not exist gives null, not an error. Takes a pid only; for a systemd service get its pid first, e.g. `run systemctl show -p MainPID --value plang`. e.g. `get process stats for pid %pid%, write to %p%`


Examples:

- get process stats for pid %pid%, write to %p% => pid=%pid%
- get stats of process 1234, write to %process% => pid=1234
- get process stats %pid%, write to %p% => pid=%pid%

### GetTcpConnections

```
GetTcpConnections() : List<TcpConnection>
```

Every TCP connection and listening socket as a list, the long form of `get tcp stats`. Each row: Protocol, State (established, time_wait, close_wait, listen ...), LocalAddress, LocalPort, RemoteAddress, RemotePort, RxQueue, TxQueue, Uid. A busy server has thousands of rows: use `get tcp stats` when the numbers are enough. e.g. `get tcp stats full, write to %connections%`


Examples:

- get tcp stats full, write to %connections% =>
- list every tcp connection, write to %connections% =>

### GetTcpStats

```
GetTcpStats() : PLang.Modules.HostModule.TcpStats
```

The TCP connections now counted by state, and the kernel's TCP counters since boot. One object: Established, SynRecv, TimeWait, CloseWait, Total, ListenQueueMax (the most connections waiting for accept on a listening socket), RetransSegs, AttemptFails, EstabResets, ListenOverflows, ListenDrops, Timeouts, InSegs, OutSegs (segments received and sent), InErrs. The counters only go up: take the difference between two reads; retransmits as a share of traffic = ΔRetransSegs / ΔOutSegs. e.g. `get tcp stats, write to %tcp%`


Examples:

- get tcp stats, write to %tcp% =>
- how many tcp connections are open, write to %tcp% =>

