#!/usr/bin/env python3

import curses
import json
import os
import subprocess
from pathlib import Path


HOSTS_FILE = "hosts.json"


class MenuItem:
    def __init__(self, title, action=None, submenu=None):
        self.title = title
        self.action = action
        self.submenu = submenu


class CursesMenu:
    def __init__(self, stdscr, title, items):
        self.stdscr = stdscr
        self.title = title
        self.items = items
        self.selected = 0

    def draw(self):
        self.stdscr.clear()
        height, width = self.stdscr.getmaxyx()

        title = f" {self.title} "
        self.stdscr.addstr(1, max(0, (width - len(title)) // 2), title, curses.A_BOLD)

        help_text = "↑/↓ Move | Enter Select | Backspace Back | Q Quit"
        self.stdscr.addstr(height - 2, max(0, (width - len(help_text)) // 2), help_text, curses.A_DIM)

        start_y = 4

        for index, item in enumerate(self.items):
            marker = "➜ " if index == self.selected else "  "
            line = f"{marker}{item.title}"

            x = max(2, (width - len(line)) // 2)
            y = start_y + index

            if index == self.selected:
                self.stdscr.attron(curses.A_REVERSE)
                self.stdscr.addstr(y, x, line[:width - 4])
                self.stdscr.attroff(curses.A_REVERSE)
            else:
                self.stdscr.addstr(y, x, line[:width - 4])

        self.stdscr.refresh()

    def run(self):
        while True:
            self.draw()
            key = self.stdscr.getch()

            if key in [curses.KEY_UP, ord("k")]:
                self.selected = (self.selected - 1) % len(self.items)

            elif key in [curses.KEY_DOWN, ord("j")]:
                self.selected = (self.selected + 1) % len(self.items)

            elif key in [10, 13, curses.KEY_ENTER]:
                item = self.items[self.selected]

                if item.submenu:
                    submenu = CursesMenu(self.stdscr, item.title, item.submenu)
                    submenu.run()

                elif item.action:
                    item.action(self.stdscr)

            elif key in [ord("q"), ord("Q")]:
                raise SystemExit

            elif key in [curses.KEY_BACKSPACE, 127, 8]:
                return


def load_hosts():
    path = Path(HOSTS_FILE)

    if not path.exists():
        return []

    with open(path, "r", encoding="utf-8") as file:
        return json.load(file)


def pause(stdscr, message="Press any key to continue..."):
    stdscr.addstr(curses.LINES - 3, 2, message, curses.A_DIM)
    stdscr.refresh()
    stdscr.getch()


def show_message(stdscr, title, lines):
    stdscr.clear()
    stdscr.addstr(1, 2, title, curses.A_BOLD)

    for index, line in enumerate(lines, start=3):
        stdscr.addstr(index, 2, line[: curses.COLS - 4])

    pause(stdscr)


def connect_ssh(host_entry):
    def action(stdscr):
        user = host_entry["user"]
        host = host_entry["host"]
        key = os.path.expanduser(host_entry["key"])
        port = str(host_entry.get("port", 22))

        ssh_command = [
            "ssh",
            "-i",
            key,
            "-p",
            port,
            f"{user}@{host}"
        ]

        curses.endwin()

        print()
        print(f"Connecting to {host_entry['name']}...")
        print("Command:")
        print(" ".join(ssh_command))
        print()

        try:
            subprocess.run(ssh_command)
        finally:
            input("\nSSH session ended. Press Enter to return to menu...")

    return action


def build_aws_menu():
    hosts = load_hosts()

    if not hosts:
        return [
            MenuItem(
                "No hosts found in hosts.json",
                action=lambda stdscr: show_message(
                    stdscr,
                    "No AWS Hosts Found",
                    [
                        "Create a hosts.json file next to this script.",
                        "Add your AWS SSH host, user, key, and port.",
                        "",
                        "Example:",
                        "{",
                        '  "name": "AWS Web Server",',
                        '  "user": "ubuntu",',
                        '  "host": "ec2-x-x-x-x.compute.amazonaws.com",',
                        '  "key": "~/.ssh/aws.pem",',
                        '  "port": 22',
                        "}"
                    ]
                )
            )
        ]

    menu_items = []

    for host in hosts:
        title = f"{host['name']} - {host['user']}@{host['host']}"
        menu_items.append(MenuItem(title, action=connect_ssh(host)))

    return menu_items


def placeholder_action(name):
    def action(stdscr):
        show_message(
            stdscr,
            name,
            [
                f"{name} module is not built yet.",
                "",
                "This is where you can add future framework actions.",
                "Examples:",
                "- Run Ansible playbooks",
                "- Check server uptime",
                "- Restart services",
                "- Pull logs",
                "- Open monitoring dashboards"
            ]
        )

    return action


def main(stdscr):
    curses.curs_set(0)
    stdscr.keypad(True)

    if curses.has_colors():
        curses.start_color()

    main_menu = [
        MenuItem("Connect to AWS with SSH", submenu=build_aws_menu()),
        MenuItem("AWS Tools", submenu=[
            MenuItem("Check Instance Status", action=placeholder_action("Check Instance Status")),
            MenuItem("View Server Logs", action=placeholder_action("View Server Logs")),
            MenuItem("Run Maintenance Task", action=placeholder_action("Run Maintenance Task")),
        ]),
        MenuItem("Settings", submenu=[
            MenuItem("Show Config Location", action=lambda stdscr: show_message(
                stdscr,
                "Config Location",
                [
                    f"Hosts file: {os.path.abspath(HOSTS_FILE)}",
                    "",
                    "Your SSH keys should stay in ~/.ssh/",
                    "Do not store private key contents inside this script."
                ]
            ))
        ]),
        MenuItem("Exit", action=lambda stdscr: raise_exit())
    ]

    menu = CursesMenu(stdscr, "Seven Labs AWS ncurses Framework", main_menu)
    menu.run()


def raise_exit():
    raise SystemExit


if __name__ == "__main__":
    try:
        curses.wrapper(main)
    except SystemExit:
        print("Goodbye.")
