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

    _countWordsInString(str) {
        const words = str.match(/\S+/g);
        return words ? words.length : 0;
    }

    append(fullText) {
        if (!fullText) return;
        if (fullText.length <= this._fullText.length) return;

        const oldLength = this._fullText.length;
        const addedText = fullText.slice(oldLength);

        this._remainingWords += this._countWordsInString(addedText);

        this._fullText = fullText;
    }

    readNext(wordsPerRead = this._defaultWordsPerRead) {
        if (this._cursor >= this._fullText.length) return this._visibleText;

        const remaining = this._fullText.slice(this._cursor);

        const tokens = remaining.match(/\s+|\S+/g) || [];

        let wordsTaken = 0;
        const tokensToTake = [];
        for (let i = 0; i < tokens.length && wordsTaken < wordsPerRead; i++) {
            const t = tokens[i];
            tokensToTake.push(t);
            if (!/^\s+$/.test(t)) wordsTaken++;
        }
        const chunk = tokensToTake.join('');

        this._visibleText += chunk;
        this._cursor += chunk.length;

        this._remainingWords = Math.max(0, this._remainingWords - wordsTaken);

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