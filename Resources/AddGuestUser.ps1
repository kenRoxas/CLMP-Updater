param(
    [string]$Username,
    [string]$Password
)
$Username = $Username -replace '-',''
net.exe user $Username $Password /add
net.exe localgroup Guests $Username /add
net.exe localgroup 'Remote Desktop Users' $Username /add