# SongStatus
A Beat Saber plugin to write currently playing song status to a text file that can be read by programs like OBS.

Writes to songStatus.txt in your Beat Saber UserData folder. Customize the output in songStatusTemplate.txt

## Available tags
* {songName} 
* {songSubName}
* {authorName}
* ~~{gamemode}~~ Temporarily removed
* {difficulty}
* {beatsPerMinute}
* {[isNoFail]}
* {[modifiers]}
* {notesCount}
* {obstaclesCount}
