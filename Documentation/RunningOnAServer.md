# Running a plang app on your own server

A plang app is a folder of `.goal` files, a `.build` folder and a runtime. Putting that on a Linux
box and serving a domain from it takes an afternoon the first time and half an hour after that.
This page is the shape that works, written after doing it for real, including the parts that only
show up in production.

The setup here serves TLS directly from plang with no reverse proxy, deploys by pulling a git
branch, and runs as a system user with no shell and no sudo.

## What runs where

**Build on your machine, never on the server.** Building calls an llm, and the server has no
business holding an llm key. What you deploy is the `.goal` files together with the `.build` folder
that was produced from them, so a build failure can never take the site down and the server needs
no api key at all.

Two pieces go up: the **runtime**, which changes rarely, and the **app**, which changes constantly.
Keep them apart.

## The runtime

Publish framework dependent if .NET is on the box, self contained if it is not:

```bash
dotnet publish PlangConsole/PLangConsole.csproj -c Release -r linux-x64 --self-contained false -o out/plang
cp -a os system out/plang/          # both, or the builder files are missing at runtime
tar czf plang-$(cat PLang/version.txt)-linux-x64.tar.gz -C out plang
```

On the server, install each version into its own directory with a `current` symlink, so an upgrade
and a rollback are the same one line:

```bash
sudo mkdir -p /opt/plang/$V
sudo tar xzf /tmp/plang-$V-linux-x64.tar.gz -C /tmp && sudo cp -a /tmp/plang/. /opt/plang/$V/
sudo ln -sfn /opt/plang/$V /opt/plang/current
printf '#!/bin/sh\nexec dotnet /opt/plang/current/plang.dll "$@"\n' | sudo tee /usr/local/bin/plang
sudo chmod +x /usr/local/bin/plang
```

Write that wrapper with `printf`, not `echo`. `echo '\n'` prints a literal backslash n in some
shells and you get a wrapper that does not run.

**`plang --version` fails when run from the runtime directory.** It wants to create `.db` in the
working directory and that directory is not writable. Run it from the app directory. This is not a
fault.

## The app

First time up, before any deploy key exists, ship exactly what is in version control:

```bash
git archive --format=tar.gz -o app.tar.gz HEAD
```

`git archive HEAD` is the right tool because it respects `.gitignore`, which should let `.build` in
and keep `.db` out. **Commit `.build`.** It is the compiled program.

The first run creates the databases. Do it as the service user, not as yourself, or the files end
up owned by the wrong account:

```bash
sudo -u appuser bash -c "cd /srv/example.com && timeout 90 plang"
```

For deploys after that, generate an ssh key **on the server**, put the public half on the repository
as a deploy key **without write access**, and have a small script do the pull:

```bash
git -C "$APP" fetch --quiet origin
git -C "$APP" reset --hard --quiet origin/main
systemctl restart app.service
sleep 8
test "$(curl -sk -o /dev/null -w '%{http_code}' https://127.0.0.1/)" = "200"
```

Deploying should be a decision, not something that happens on push. The last line matters: a deploy
that does not check the site afterwards is not a deploy, it is a hope.

## systemd

```ini
[Service]
User=appuser
WorkingDirectory=/srv/example.com
ExecStart=/usr/local/bin/plang live=1
Restart=always
RestartSec=2s

AmbientCapabilities=CAP_NET_BIND_SERVICE
CapabilityBoundingSet=CAP_NET_BIND_SERVICE
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ReadWritePaths=/srv/example.com /home/appuser
ProtectKernelTunables=true
ProtectKernelModules=true
RestrictAddressFamilies=AF_INET AF_INET6 AF_UNIX
MemoryHigh=1300M
MemoryMax=1600M
```

Three things earn their place:

- `AmbientCapabilities=CAP_NET_BIND_SERVICE` is what lets an unprivileged user bind 443. Without it
  you need root or sudo, and then the hardening is gone.
- `ProtectSystem=strict` with `ReadWritePaths` for the app directory and the home directory. This is
  the line that breaks things, so restart and make one request immediately after adding it.
- Memory limits, because a runaway app on a small box takes ssh down with it.

`--llmservice=...` does **not** belong in `ExecStart`. Nothing on the web server should be able to
call an llm.

## TLS without a proxy

Nothing listens on port 80 in normal operation, so certbot standalone works:

```bash
sudo certbot certonly --standalone -d example.com -d www.example.com --key-type ecdsa \
     --agree-tos -m you@example.com --non-interactive
```

```plang
SetCertificate
- set web certificate "/etc/letsencrypt/live/example.com/fullchain.pem",
                      "/etc/letsencrypt/live/example.com/privkey.pem"
```

**Two renewal hooks, both required.** These are the ones people find out about three months later,
when the certificate first renews:

`/etc/letsencrypt/renewal-hooks/deploy/10-fix-perms.sh` gives the `ssl-cert` group read access,
because the service does not run as root. Renewal resets the private key to 600 root, and without
this hook the app simply fails to start the next time it restarts.

`/etc/letsencrypt/renewal-hooks/deploy/20-restart-app.sh` restarts the service, because **plang
reads the certificate once, at webserver start**. Renewal on disk does not reach a running process,
so without this hook the app keeps serving the expired certificate until something else restarts it.

Put the service user in the `ssl-cert` group.

## A watchdog, and how not to fight it

A systemd timer every few minutes that curls `https://127.0.0.1/` and restarts the service if it
does not answer. Two details:

- Have the deploy script drop a lock file and have the watchdog skip while it exists, otherwise the
  two of them restart the service on top of each other.
- **Do not enable the timer before the certificate exists.** It probes https, fails, restarts,
  forever.

Log a line before restarting. A watchdog that heals silently hides the fault you needed to see.

## Backups and rollback

| What broke | Way back |
| --- | --- |
| A runtime version | `ln -sfn /opt/plang/<previous> /opt/plang/current` and restart |
| An app version | `git reset --hard <sha>` in the app directory and restart |
| A certificate | The old one is still in `/etc/letsencrypt/archive/`, certbot does not replace it until the new one is valid |
| The machine | Provider snapshot. Note that a snapshot of a live SQLite file is crash consistent, not clean |

If you copy a SQLite database off a running server, copy the `-wal` and `-shm` files with it or use
`sqlite3 .backup`. A bare copy of the `.sqlite` file can be missing the most recent writes.

## Checks that actually prove it works

```bash
curl -sI https://example.com | head -1          # 200
sudo ss -lntp                                   # 443 and nothing else public
systemctl is-enabled app.service                # enabled
sudo certbot certificates                       # right domains, right expiry
systemctl list-timers | grep -E "certbot|watchdog"
```

Then restart the machine once, before you have users, and watch it come back on its own.

## See also

- [Startup parameters](./StartupParameters.md) for `live=1` and the rest.
- [Identity in the browser](./IdentityInTheBrowser.md) if the app serves people rather than machines.
