/*
 * Copyright (C) 2024 Jami Suni
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/gpl-3.0.en.html>.
 */

namespace Pfs.Types;

public class Note
{
    protected string _content;

    public Note()
    {
        _content = string.Empty;
    }

    public Note(string content)
    {
        _content = content;
    }

    public string Get()
    {
        return _content;
    }

    public string GetStorageFormat()
    {
        return _content;
    }

    // For public reports, by adding overview or bodyText a [* text here, multilines ok *] can fetch only that text to be shown on reports
    public string GetPublicNote()
    {
        int start;
        int end;

        if (string.IsNullOrWhiteSpace(_content) == false)
        {
            start = _content.IndexOf("[*");
            end = _content.IndexOf("*]");

            if (start >= 0 && end >= 0 && end - start >= 5)
                return _content.Substring(start + 2, end - start - 2);
        }

        return string.Empty;
    }
}