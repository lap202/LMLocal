/**
* A small buffer that accumulates incoming text chunks and exposes a
* "visible" assembled text in controlled increments. 
**/
export class StreamingBuffer {

    constructor(wordsPerRead = 2) {
        this._fullText = "";
        this._cursor = 0;
        this._visibleText = "";
        this._remainingWords = 0;
        this._defaultWordsPerRead = wordsPerRead;
    }

    _countWords(str) {
        let count = 0;
        let inWord = false;
        for (let i = 0; i < str.length; i++) {
            const ch = str[i];
            const isSpace = ch === ' ' || ch === '\n' || ch === '\r' || ch === '\t';

            if (!isSpace) {
                if (!inWord) {
                    count++;
                    inWord = true;
                }
            } else {
                inWord = false;
            }
        }

        return count;
    }

    append(fullText) {
        if (!fullText || fullText.length <= this._fullText.length) return;

        const oldLen = this._fullText.length;
        const newPart = fullText.slice(oldLen);

        let addedWords = this._countWords(newPart);

        if (oldLen > 0 && addedWords > 0) {
            const lastCharOld = this._fullText[oldLen - 1];
            const firstCharNew = newPart[0];
            const isOldCharWord = !(lastCharOld === ' ' || lastCharOld === '\n' || lastCharOld === '\r' || lastCharOld === '\t');
            const isNewCharWord = !(firstCharNew === ' ' || firstCharNew === '\n' || firstCharNew === '\r' || firstCharNew === '\t');

            if (isOldCharWord && isNewCharWord) {
                addedWords--;
            }
        }

        this._remainingWords += addedWords;
        this._fullText = fullText;
    }

    readNext(wordsPerRead = this._defaultWordsPerRead) {
        if (this._cursor >= this._fullText.length) return this._visibleText;

        let wordsFound = 0;
        let idx = this._cursor;
        const len = this._fullText.length;

        const isSpace = (i) => {
            const ch = this._fullText[i];
            return ch === ' ' || ch === '\n' || ch === '\r' || ch === '\t';
        };

        while (idx < len && wordsFound < wordsPerRead) {
            while (idx < len && isSpace(idx)) {
                idx++;
            }

            if (idx >= len) break;

            while (idx < len && !isSpace(idx)) {
                idx++;
            }

            wordsFound++;
        }

        const chunk = this._fullText.substring(this._cursor, idx);

        this._visibleText += chunk;
        this._cursor = idx;
        this._remainingWords = Math.max(0, this._remainingWords - wordsFound);

        return this._visibleText;
    }

    getRemainingWordsCount() {
        return this._remainingWords;
    }

    flush() {
        if (this._cursor < this._fullText.length) {
            this._visibleText = this._fullText;
            this._cursor = this._fullText.length;
            this._remainingWords = 0;
        }
        return this._visibleText;
    }

    reset() {
        this._fullText = "";
        this._cursor = 0;
        this._visibleText = "";
        this._remainingWords = 0;
    }

    get visibleText() {
        return this._visibleText;
    }
} 